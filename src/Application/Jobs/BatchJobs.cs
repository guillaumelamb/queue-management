using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using QueueManagement.Domain;
using QueueManagement.Domain.Cache;
using QueueManagement.Domain.Entities;
using QueueManagement.Domain.Repositories;

namespace QueueManagement.Application.Jobs
{
    /// <summary>
    /// Result of a job execution.
    /// </summary>
    public sealed class JobResult
    {
        public bool Success { get; init; }
        public int ProcessedCount { get; init; }
        public string Message { get; init; } = string.Empty;
        public TimeSpan Duration { get; init; }
    }

    /// <summary>
    /// Job to compute queue ranks from pre-registrations (lottery).
    /// Uses Fisher-Yates shuffle with cryptographic RNG for fairness.
    /// </summary>
    public sealed class ComputeQueueRanksJob
    {
        private readonly IPreRegistrationRepository _preRegistrationRepository;
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheService _cacheService;

        public ComputeQueueRanksJob(
            IPreRegistrationRepository preRegistrationRepository,
            IQueueEntryRepository queueEntryRepository,
            IEventRepository eventRepository,
            ICacheService cacheService)
        {
            _preRegistrationRepository = preRegistrationRepository;
            _queueEntryRepository = queueEntryRepository;
            _eventRepository = eventRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Execute the lottery for an event.
        /// Idempotent: will not overwrite existing ranks.
        /// </summary>
        public async Task<JobResult> ExecuteAsync(Guid eventId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Check if ranks already computed
            if (await _queueEntryRepository.HasRanksAsync(eventId))
            {
                return new JobResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    Message = "Ranks already computed for this event.",
                    Duration = stopwatch.Elapsed
                };
            }

            // Get all pre-registrations
            var preRegistrations = await _preRegistrationRepository.GetByEventAsync(eventId);
            if (preRegistrations.Count == 0)
            {
                return new JobResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    Message = "No pre-registrations to process.",
                    Duration = stopwatch.Elapsed
                };
            }

            // Create list for shuffling
            var userIds = preRegistrations.Select(p => p.UserId).ToList();

            // Fisher-Yates shuffle with cryptographic RNG
            CryptoShuffle(userIds);

            // Generate queue entries with ranks
            var queueEntries = new List<QueueEntry>();
            for (int i = 0; i < userIds.Count; i++)
            {
                queueEntries.Add(new QueueEntry
                {
                    EventId = eventId,
                    UserId = userIds[i],
                    Rank = i + 1, // 1-based ranking
                    Status = QueueEntryStatus.Pending
                });
            }

            // Bulk insert to database
            await _queueEntryRepository.CreateBulkAsync(queueEntries);

            // Populate Redis ZSET
            var cacheEntries = queueEntries
                .Select(e => (e.UserId, e.Rank))
                .ToArray();
            await _cacheService.QueueAddBulkAsync(eventId, cacheEntries);

            stopwatch.Stop();

            return new JobResult
            {
                Success = true,
                ProcessedCount = userIds.Count,
                Message = $"Successfully computed ranks for {userIds.Count} participants.",
                Duration = stopwatch.Elapsed
            };
        }

        /// <summary>
        /// Fisher-Yates shuffle using cryptographic RNG for unbiased randomness.
        /// </summary>
        private static void CryptoShuffle<T>(IList<T> list)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];

            for (int i = list.Count - 1; i > 0; i--)
            {
                rng.GetBytes(bytes);
                int j = (int)(BitConverter.ToUInt32(bytes, 0) % (uint)(i + 1));
                
                // Swap
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// Job to invite participants from the queue based on available capacity.
    /// Processes participants in rank order.
    /// </summary>
    public sealed class InviteParticipantsJob
    {
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheService _cacheService;

        public InviteParticipantsJob(
            IQueueEntryRepository queueEntryRepository,
            IEventRepository eventRepository,
            ICacheService cacheService)
        {
            _queueEntryRepository = queueEntryRepository;
            _eventRepository = eventRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Invite participants based on available capacity.
        /// </summary>
        public async Task<JobResult> ExecuteAsync(Guid eventId, int? batchSize = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return new JobResult
                {
                    Success = false,
                    Message = "Event not found."
                };
            }

            // Calculate how many to invite
            int capacityRemaining = evt.CapacityRemaining;
            int toInvite = batchSize.HasValue 
                ? Math.Min(batchSize.Value, capacityRemaining) 
                : capacityRemaining;

            if (toInvite <= 0)
            {
                return new JobResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    Message = "No capacity available for invitations.",
                    Duration = stopwatch.Elapsed
                };
            }

            // Get pending entries up to capacity
            var pendingEntries = await _queueEntryRepository.GetByEventAndStatusAsync(eventId, QueueEntryStatus.Pending);
            var toProcess = pendingEntries.Take(toInvite).ToList();

            if (toProcess.Count == 0)
            {
                return new JobResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    Message = "No pending participants to invite.",
                    Duration = stopwatch.Elapsed
                };
            }

            // Update status to INVITED
            foreach (var entry in toProcess)
            {
                entry.Status = QueueEntryStatus.Invited;
                await _queueEntryRepository.UpdateAsync(entry);
            }

            stopwatch.Stop();

            return new JobResult
            {
                Success = true,
                ProcessedCount = toProcess.Count,
                Message = $"Invited {toProcess.Count} participants.",
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Job to expire reservations that have timed out.
    /// Should run periodically (every 1-2 minutes).
    /// </summary>
    public sealed class ExpireReservationsJob
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly ICacheService _cacheService;

        public ExpireReservationsJob(
            IReservationRepository reservationRepository,
            IEventRepository eventRepository,
            IQueueEntryRepository queueEntryRepository,
            ICacheService cacheService)
        {
            _reservationRepository = reservationRepository;
            _eventRepository = eventRepository;
            _queueEntryRepository = queueEntryRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Expire pending reservations that have passed their deadline.
        /// </summary>
        public async Task<JobResult> ExecuteAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var now = DateTime.UtcNow;

            // Get expired reservations
            var expiredReservations = await _reservationRepository.GetExpiredPendingAsync(now);

            if (expiredReservations.Count == 0)
            {
                return new JobResult
                {
                    Success = true,
                    ProcessedCount = 0,
                    Message = "No expired reservations to process.",
                    Duration = stopwatch.Elapsed
                };
            }

            int processed = 0;
            var eventsToUpdate = new HashSet<Guid>();

            foreach (var reservation in expiredReservations)
            {
                // Update reservation status
                reservation.Status = ReservationStatus.Expired;
                await _reservationRepository.UpdateAsync(reservation);

                // Release capacity
                await _eventRepository.DecrementReservedAsync(reservation.EventId, 1);
                eventsToUpdate.Add(reservation.EventId);

                // Update cache
                await _cacheService.ReservationDeleteAsync(reservation.EventId, reservation.UserId);
                await _cacheService.CapacityDecrementReservedAsync(reservation.EventId, 1);

                processed++;
            }

            stopwatch.Stop();

            return new JobResult
            {
                Success = true,
                ProcessedCount = processed,
                Message = $"Expired {processed} reservations across {eventsToUpdate.Count} events.",
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Job to synchronize cache with database (warmup or recovery).
    /// </summary>
    public sealed class SyncCacheJob
    {
        private readonly IEventRepository _eventRepository;
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly ICacheService _cacheService;

        public SyncCacheJob(
            IEventRepository eventRepository,
            IQueueEntryRepository queueEntryRepository,
            IReservationRepository reservationRepository,
            ICacheService cacheService)
        {
            _eventRepository = eventRepository;
            _queueEntryRepository = queueEntryRepository;
            _reservationRepository = reservationRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Sync cache for a specific event.
        /// </summary>
        public async Task<JobResult> ExecuteAsync(Guid eventId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return new JobResult
                {
                    Success = false,
                    Message = "Event not found."
                };
            }

            // Sync capacity
            await _cacheService.CapacitySetAsync(eventId, new CachedCapacity
            {
                CapacityTotal = evt.CapacityTotal,
                CapacityReserved = evt.CapacityReserved,
                CapacityConfirmed = evt.CapacityConfirmed
            });

            // Sync queue
            await _cacheService.QueueDeleteAsync(eventId);
            var queueEntries = await _queueEntryRepository.GetByEventAsync(eventId);
            if (queueEntries.Count > 0)
            {
                var cacheEntries = queueEntries
                    .Select(e => (e.UserId, e.Rank))
                    .ToArray();
                await _cacheService.QueueAddBulkAsync(eventId, cacheEntries);
            }

            stopwatch.Stop();

            return new JobResult
            {
                Success = true,
                ProcessedCount = queueEntries.Count,
                Message = $"Synced cache for event. Queue entries: {queueEntries.Count}",
                Duration = stopwatch.Elapsed
            };
        }
    }
}
