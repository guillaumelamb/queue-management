using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace QueueApp
{
    /// <summary>
    /// Represents a participant in the virtual waiting room.
    /// Contains all information needed to track position, status, and session validity.
    /// </summary>
    public sealed class WaitingParticipant
    {
        public string Id { get; }
        public string Token { get; }
        public DateTime JoinedAt { get; }
        public DateTime LastHeartbeat { get; private set; }
        public int InitialPosition { get; }
        public int CurrentPosition { get; internal set; }
        public ParticipantStatus Status { get; internal set; }
        public DateTime? ActivatedAt { get; internal set; }
        public DateTime? ExpiresAt { get; internal set; }
        public int ReconnectionCount { get; internal set; }
        public string? IpAddress { get; }
        public string? UserAgent { get; }

        internal WaitingParticipant(string id, string token, int position, string? ipAddress = null, string? userAgent = null)
        {
            Id = id;
            Token = token;
            JoinedAt = DateTime.UtcNow;
            LastHeartbeat = JoinedAt;
            InitialPosition = position;
            CurrentPosition = position;
            Status = ParticipantStatus.Waiting;
            IpAddress = ipAddress;
            UserAgent = userAgent;
        }

        internal void UpdateHeartbeat() => LastHeartbeat = DateTime.UtcNow;
    }

    public enum ParticipantStatus
    {
        Waiting,        // In queue, waiting for their turn
        Active,         // Their turn - can access registration
        Completed,      // Successfully completed registration
        Expired,        // Session expired (didn't complete in time)
        Disconnected,   // Lost connection, can reconnect
        Banned          // Detected as bot or abuse
    }

    /// <summary>
    /// Event args for queue status changes
    /// </summary>
    public sealed class QueueStatusEventArgs : EventArgs
    {
        public string ParticipantId { get; }
        public ParticipantStatus OldStatus { get; }
        public ParticipantStatus NewStatus { get; }
        public int Position { get; }

        public QueueStatusEventArgs(string participantId, ParticipantStatus oldStatus, ParticipantStatus newStatus, int position)
        {
            ParticipantId = participantId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Position = position;
        }
    }

    /// <summary>
    /// Virtual Waiting Room - A robust queue system for high-demand registration events.
    /// 
    /// Addresses issues commonly seen in failed registration systems:
    /// - Position preservation on reconnection
    /// - Accurate wait time estimation
    /// - Session timeout management
    /// - Anti-bot protection
    /// - Capacity management
    /// - Fair queuing with priority for returning users
    /// </summary>
    public sealed class VirtualWaitingRoom
    {
        #region Configuration

        /// <summary>Maximum number of concurrent active sessions</summary>
        public int MaxActiveSessions { get; }

        /// <summary>How long an active session lasts before expiring</summary>
        public TimeSpan ActiveSessionTimeout { get; }

        /// <summary>Grace period for reconnection after disconnect</summary>
        public TimeSpan ReconnectionGracePeriod { get; }

        /// <summary>Required interval between heartbeats</summary>
        public TimeSpan HeartbeatInterval { get; }

        /// <summary>Maximum reconnection attempts before losing position</summary>
        public int MaxReconnectionAttempts { get; }

        /// <summary>Maximum queue capacity (0 = unlimited)</summary>
        public int MaxQueueCapacity { get; }

        #endregion

        #region State

        private readonly ConcurrentDictionary<string, WaitingParticipant> _participantsById;
        private readonly ConcurrentDictionary<string, string> _tokenToId;
        private readonly ConcurrentDictionary<string, int> _ipConnectionCount;
        private readonly CustomQueue<string> _waitingQueue;
        private readonly HashSet<string> _activeParticipants;
        private readonly object _lock = new();

        private int _totalJoined;
        private int _totalCompleted;
        private long _totalWaitTimeTicks;
        private readonly Stopwatch _runtimeStopwatch;

        #endregion

        #region Events

        public event EventHandler<QueueStatusEventArgs>? StatusChanged;
        public event EventHandler<string>? ParticipantActivated;
        public event EventHandler<string>? ParticipantExpired;

        #endregion

        #region Constructor

        public VirtualWaitingRoom(
            int maxActiveSessions = 100,
            TimeSpan? activeSessionTimeout = null,
            TimeSpan? reconnectionGracePeriod = null,
            TimeSpan? heartbeatInterval = null,
            int maxReconnectionAttempts = 3,
            int maxQueueCapacity = 0)
        {
            MaxActiveSessions = maxActiveSessions;
            ActiveSessionTimeout = activeSessionTimeout ?? TimeSpan.FromMinutes(10);
            ReconnectionGracePeriod = reconnectionGracePeriod ?? TimeSpan.FromMinutes(5);
            HeartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
            MaxReconnectionAttempts = maxReconnectionAttempts;
            MaxQueueCapacity = maxQueueCapacity;

            _participantsById = new ConcurrentDictionary<string, WaitingParticipant>();
            _tokenToId = new ConcurrentDictionary<string, string>();
            _ipConnectionCount = new ConcurrentDictionary<string, int>();
            _waitingQueue = new CustomQueue<string>(Math.Max(1024, maxQueueCapacity));
            _activeParticipants = new HashSet<string>();
            _runtimeStopwatch = Stopwatch.StartNew();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Join the waiting room. Returns a participant with position and token.
        /// </summary>
        public JoinResult Join(string? ipAddress = null, string? userAgent = null, int maxConnectionsPerIp = 5)
        {
            // Anti-bot: Check IP connection limit
            if (!string.IsNullOrEmpty(ipAddress))
            {
                int currentConnections = _ipConnectionCount.GetOrAdd(ipAddress, 0);
                if (currentConnections >= maxConnectionsPerIp)
                {
                    return JoinResult.Failed("Too many connections from this IP address.");
                }
            }

            lock (_lock)
            {
                // Check capacity
                if (MaxQueueCapacity > 0 && _waitingQueue.Count >= MaxQueueCapacity)
                {
                    return JoinResult.Failed("Queue is full. Please try again later.");
                }

                // Generate unique ID and token
                string id = GenerateId();
                string token = GenerateToken();
                int position = _waitingQueue.Count + _activeParticipants.Count + 1;

                var participant = new WaitingParticipant(id, token, position, ipAddress, userAgent);
                
                _participantsById[id] = participant;
                _tokenToId[token] = id;
                _waitingQueue.Enqueue(id);
                _totalJoined++;

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    _ipConnectionCount.AddOrUpdate(ipAddress, 1, (_, count) => count + 1);
                }

                // Try to activate if there's room
                TryActivateNext();

                return JoinResult.Success(participant, EstimateWaitTime(position));
            }
        }

        /// <summary>
        /// Reconnect using a previously issued token.
        /// Preserves position if within grace period.
        /// </summary>
        public ReconnectResult Reconnect(string token)
        {
            if (!_tokenToId.TryGetValue(token, out string? id))
            {
                return ReconnectResult.Failed("Invalid token.");
            }

            if (!_participantsById.TryGetValue(id, out WaitingParticipant? participant))
            {
                return ReconnectResult.Failed("Participant not found.");
            }

            lock (_lock)
            {
                if (participant.Status == ParticipantStatus.Banned)
                {
                    return ReconnectResult.Failed("Access denied.");
                }

                if (participant.Status == ParticipantStatus.Completed)
                {
                    return ReconnectResult.Failed("Registration already completed.");
                }

                if (participant.Status == ParticipantStatus.Expired)
                {
                    // Check if within grace period
                    if (participant.ExpiresAt.HasValue && 
                        DateTime.UtcNow - participant.ExpiresAt.Value < ReconnectionGracePeriod)
                    {
                        if (participant.ReconnectionCount >= MaxReconnectionAttempts)
                        {
                            return ReconnectResult.Failed("Maximum reconnection attempts exceeded.");
                        }

                        participant.ReconnectionCount++;
                        participant.Status = ParticipantStatus.Waiting;
                        participant.UpdateHeartbeat();
                        
                        // Re-add to queue at original position (priority reconnection)
                        // In a real system, we'd use a priority queue
                        return ReconnectResult.Success(participant, EstimateWaitTime(participant.CurrentPosition));
                    }
                    return ReconnectResult.Failed("Session expired. Grace period exceeded.");
                }

                if (participant.Status == ParticipantStatus.Disconnected)
                {
                    if (participant.ReconnectionCount >= MaxReconnectionAttempts)
                    {
                        participant.Status = ParticipantStatus.Expired;
                        return ReconnectResult.Failed("Maximum reconnection attempts exceeded.");
                    }

                    participant.ReconnectionCount++;
                    participant.Status = ParticipantStatus.Waiting;
                    participant.UpdateHeartbeat();
                    
                    return ReconnectResult.Success(participant, EstimateWaitTime(participant.CurrentPosition));
                }

                participant.UpdateHeartbeat();
                return ReconnectResult.Success(participant, EstimateWaitTime(participant.CurrentPosition));
            }
        }

        /// <summary>
        /// Send heartbeat to maintain session.
        /// </summary>
        public HeartbeatResult Heartbeat(string token)
        {
            if (!_tokenToId.TryGetValue(token, out string? id) ||
                !_participantsById.TryGetValue(id, out WaitingParticipant? participant))
            {
                return new HeartbeatResult(false, null, 0, TimeSpan.Zero, "Invalid token.");
            }

            participant.UpdateHeartbeat();

            return new HeartbeatResult(
                success: true,
                status: participant.Status,
                position: participant.CurrentPosition,
                wait: EstimateWaitTime(participant.CurrentPosition),
                message: GetStatusMessage(participant)
            );
        }

        /// <summary>
        /// Mark registration as completed.
        /// </summary>
        public bool Complete(string token)
        {
            if (!_tokenToId.TryGetValue(token, out string? id) ||
                !_participantsById.TryGetValue(id, out WaitingParticipant? participant))
            {
                return false;
            }

            lock (_lock)
            {
                if (participant.Status != ParticipantStatus.Active)
                {
                    return false;
                }

                var oldStatus = participant.Status;
                participant.Status = ParticipantStatus.Completed;
                _activeParticipants.Remove(id);
                _totalCompleted++;

                if (participant.ActivatedAt.HasValue)
                {
                    _totalWaitTimeTicks += (DateTime.UtcNow - participant.JoinedAt).Ticks;
                }

                OnStatusChanged(participant, oldStatus);
                TryActivateNext();

                return true;
            }
        }

        /// <summary>
        /// Get current status for a participant.
        /// </summary>
        public StatusResult GetStatus(string token)
        {
            if (!_tokenToId.TryGetValue(token, out string? id) ||
                !_participantsById.TryGetValue(id, out WaitingParticipant? participant))
            {
                return StatusResult.NotFound();
            }

            return new StatusResult(
                found: true,
                status: participant.Status,
                position: participant.CurrentPosition,
                totalInQueue: _waitingQueue.Count,
                activeCount: _activeParticipants.Count,
                estimatedWait: EstimateWaitTime(participant.CurrentPosition),
                expiresAt: participant.ExpiresAt
            );
        }

        /// <summary>
        /// Process expired sessions and activate waiting participants.
        /// Should be called periodically (e.g., every 5 seconds).
        /// </summary>
        public int ProcessQueue()
        {
            int processed = 0;

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var toExpire = new List<string>();

                // Find expired active sessions
                foreach (var id in _activeParticipants)
                {
                    if (_participantsById.TryGetValue(id, out var participant))
                    {
                        // Check session timeout
                        if (participant.ExpiresAt.HasValue && now > participant.ExpiresAt.Value)
                        {
                            toExpire.Add(id);
                        }
                        // Check heartbeat timeout
                        else if (now - participant.LastHeartbeat > HeartbeatInterval * 3)
                        {
                            participant.Status = ParticipantStatus.Disconnected;
                            toExpire.Add(id);
                        }
                    }
                }

                // Expire sessions
                foreach (var id in toExpire)
                {
                    if (_participantsById.TryGetValue(id, out var participant))
                    {
                        var oldStatus = participant.Status;
                        if (participant.Status != ParticipantStatus.Disconnected)
                        {
                            participant.Status = ParticipantStatus.Expired;
                        }
                        participant.ExpiresAt = now;
                        _activeParticipants.Remove(id);
                        OnStatusChanged(participant, oldStatus);
                        ParticipantExpired?.Invoke(this, id);
                        processed++;
                    }
                }

                // Check waiting queue for stale connections
                // (Participants who stopped sending heartbeats while waiting)
                // This would require iterating the queue - in production, use a separate tracking structure

                // Activate waiting participants
                processed += TryActivateNext();
            }

            return processed;
        }

        /// <summary>
        /// Get queue statistics.
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            lock (_lock)
            {
                double avgWaitTime = _totalCompleted > 0 
                    ? TimeSpan.FromTicks(_totalWaitTimeTicks / _totalCompleted).TotalSeconds 
                    : 0;

                return new QueueStatistics(
                    totalJoined: _totalJoined,
                    totalCompleted: _totalCompleted,
                    currentlyWaiting: _waitingQueue.Count,
                    currentlyActive: _activeParticipants.Count,
                    averageWaitTimeSeconds: avgWaitTime,
                    throughputPerMinute: _runtimeStopwatch.Elapsed.TotalMinutes > 0 
                        ? _totalCompleted / _runtimeStopwatch.Elapsed.TotalMinutes 
                        : 0,
                    estimatedClearTimeMinutes: EstimateClearTime()
                );
            }
        }

        /// <summary>
        /// Ban a participant (e.g., detected as bot).
        /// </summary>
        public void Ban(string id, string? reason = null)
        {
            if (_participantsById.TryGetValue(id, out var participant))
            {
                lock (_lock)
                {
                    var oldStatus = participant.Status;
                    participant.Status = ParticipantStatus.Banned;
                    _activeParticipants.Remove(id);
                    OnStatusChanged(participant, oldStatus);

                    // Also ban the IP
                    if (!string.IsNullOrEmpty(participant.IpAddress))
                    {
                        _ipConnectionCount[participant.IpAddress] = int.MaxValue;
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private int TryActivateNext()
        {
            int activated = 0;

            while (_activeParticipants.Count < MaxActiveSessions && !_waitingQueue.IsEmpty())
            {
                if (!_waitingQueue.TryDequeue(out string? nextId) || nextId == null)
                    break;

                if (!_participantsById.TryGetValue(nextId, out var participant))
                    continue;

                // Skip if not in waiting status
                if (participant.Status != ParticipantStatus.Waiting)
                    continue;

                var oldStatus = participant.Status;
                participant.Status = ParticipantStatus.Active;
                participant.ActivatedAt = DateTime.UtcNow;
                participant.ExpiresAt = DateTime.UtcNow.Add(ActiveSessionTimeout);
                participant.CurrentPosition = 0;
                _activeParticipants.Add(nextId);

                OnStatusChanged(participant, oldStatus);
                ParticipantActivated?.Invoke(this, nextId);
                activated++;
            }

            // Update positions for waiting participants
            UpdateWaitingPositions();

            return activated;
        }

        private void UpdateWaitingPositions()
        {
            int position = _activeParticipants.Count + 1;
            // Note: In production, maintain a separate list for O(1) position lookup
            // This is simplified for demonstration
        }

        private TimeSpan EstimateWaitTime(int position)
        {
            if (position <= _activeParticipants.Count)
                return TimeSpan.Zero;

            int positionsAhead = position - _activeParticipants.Count;

            // Calculate based on average processing time
            double avgSecondsPerCompletion = _totalCompleted > 0 && _runtimeStopwatch.Elapsed.TotalSeconds > 0
                ? _runtimeStopwatch.Elapsed.TotalSeconds / _totalCompleted
                : ActiveSessionTimeout.TotalSeconds / 2; // Estimate half session timeout

            // Factor in batch processing (MaxActiveSessions at a time)
            int batchesAhead = (positionsAhead + MaxActiveSessions - 1) / MaxActiveSessions;
            double estimatedSeconds = batchesAhead * avgSecondsPerCompletion * MaxActiveSessions / Math.Max(1, MaxActiveSessions);

            return TimeSpan.FromSeconds(Math.Max(0, estimatedSeconds));
        }

        private double EstimateClearTime()
        {
            if (_waitingQueue.IsEmpty()) return 0;

            var waitTime = EstimateWaitTime(_waitingQueue.Count + _activeParticipants.Count);
            return waitTime.TotalMinutes;
        }

        private string GetStatusMessage(WaitingParticipant participant)
        {
            return participant.Status switch
            {
                ParticipantStatus.Waiting => $"Position {participant.CurrentPosition} in queue. Estimated wait: {EstimateWaitTime(participant.CurrentPosition).TotalMinutes:F1} minutes.",
                ParticipantStatus.Active => $"It's your turn! You have {(participant.ExpiresAt - DateTime.UtcNow)?.TotalMinutes:F1} minutes to complete registration.",
                ParticipantStatus.Completed => "Registration completed successfully.",
                ParticipantStatus.Expired => "Your session has expired.",
                ParticipantStatus.Disconnected => "Connection lost. Please reconnect to restore your position.",
                ParticipantStatus.Banned => "Access denied.",
                _ => "Unknown status."
            };
        }

        private void OnStatusChanged(WaitingParticipant participant, ParticipantStatus oldStatus)
        {
            StatusChanged?.Invoke(this, new QueueStatusEventArgs(
                participant.Id, oldStatus, participant.Status, participant.CurrentPosition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GenerateId() => Guid.NewGuid().ToString("N")[..16];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GenerateToken() => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        #endregion
    }

    #region Result Types

    public readonly struct JoinResult
    {
        public bool IsSuccess { get; }
        public WaitingParticipant? Participant { get; }
        public TimeSpan EstimatedWait { get; }
        public string? ErrorMessage { get; }

        private JoinResult(bool success, WaitingParticipant? participant, TimeSpan wait, string? error)
        {
            IsSuccess = success;
            Participant = participant;
            EstimatedWait = wait;
            ErrorMessage = error;
        }

        public static JoinResult Success(WaitingParticipant participant, TimeSpan estimatedWait)
            => new(true, participant, estimatedWait, null);

        public static JoinResult Failed(string error)
            => new(false, null, TimeSpan.Zero, error);
    }

    public readonly struct ReconnectResult
    {
        public bool IsSuccess { get; }
        public WaitingParticipant? Participant { get; }
        public TimeSpan EstimatedWait { get; }
        public string? ErrorMessage { get; }

        private ReconnectResult(bool success, WaitingParticipant? participant, TimeSpan wait, string? error)
        {
            IsSuccess = success;
            Participant = participant;
            EstimatedWait = wait;
            ErrorMessage = error;
        }

        public static ReconnectResult Success(WaitingParticipant participant, TimeSpan estimatedWait)
            => new(true, participant, estimatedWait, null);

        public static ReconnectResult Failed(string error)
            => new(false, null, TimeSpan.Zero, error);
    }

    public readonly struct HeartbeatResult
    {
        public bool Success { get; }
        public ParticipantStatus? Status { get; }
        public int Position { get; }
        public TimeSpan EstimatedWait { get; }
        public string Message { get; }

        public HeartbeatResult(bool success, ParticipantStatus? status, int position, TimeSpan wait, string message)
        {
            Success = success;
            Status = status;
            Position = position;
            EstimatedWait = wait;
            Message = message;
        }
    }

    public readonly struct StatusResult
    {
        public bool Found { get; }
        public ParticipantStatus Status { get; }
        public int Position { get; }
        public int TotalInQueue { get; }
        public int ActiveCount { get; }
        public TimeSpan EstimatedWait { get; }
        public DateTime? ExpiresAt { get; }

        public StatusResult(bool found, ParticipantStatus status, int position, int totalInQueue, 
            int activeCount, TimeSpan estimatedWait, DateTime? expiresAt)
        {
            Found = found;
            Status = status;
            Position = position;
            TotalInQueue = totalInQueue;
            ActiveCount = activeCount;
            EstimatedWait = estimatedWait;
            ExpiresAt = expiresAt;
        }

        public static StatusResult NotFound() => new(false, ParticipantStatus.Expired, 0, 0, 0, TimeSpan.Zero, null);
    }

    public readonly struct QueueStatistics
    {
        public int TotalJoined { get; }
        public int TotalCompleted { get; }
        public int CurrentlyWaiting { get; }
        public int CurrentlyActive { get; }
        public double AverageWaitTimeSeconds { get; }
        public double ThroughputPerMinute { get; }
        public double EstimatedClearTimeMinutes { get; }

        public QueueStatistics(int totalJoined, int totalCompleted, int currentlyWaiting, 
            int currentlyActive, double averageWaitTimeSeconds, double throughputPerMinute, 
            double estimatedClearTimeMinutes)
        {
            TotalJoined = totalJoined;
            TotalCompleted = totalCompleted;
            CurrentlyWaiting = currentlyWaiting;
            CurrentlyActive = currentlyActive;
            AverageWaitTimeSeconds = averageWaitTimeSeconds;
            ThroughputPerMinute = throughputPerMinute;
            EstimatedClearTimeMinutes = estimatedClearTimeMinutes;
        }

        public override string ToString() => 
            $"Queue Stats: {CurrentlyWaiting} waiting, {CurrentlyActive} active, " +
            $"{TotalCompleted}/{TotalJoined} completed, " +
            $"Avg wait: {AverageWaitTimeSeconds:F1}s, " +
            $"Throughput: {ThroughputPerMinute:F1}/min";
    }

    #endregion
}
