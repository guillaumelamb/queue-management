using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QueueManagement.Domain;
using QueueManagement.Domain.Cache;

namespace QueueManagement.Infrastructure.Cache
{
    /// <summary>
    /// In-memory implementation of ICacheService for testing/demo.
    /// In production, replace with Redis implementation using StackExchange.Redis.
    /// </summary>
    public sealed class InMemoryCacheService : ICacheService
    {
        #region Data Structures

        // Queue: event_id -> sorted dictionary of (rank -> user_id)
        private readonly ConcurrentDictionary<Guid, SortedDictionary<int, Guid>> _queues = new();
        private readonly ConcurrentDictionary<Guid, Dictionary<Guid, int>> _queueReverse = new();

        // Reservations: (event_id, user_id) -> CachedReservation
        private readonly ConcurrentDictionary<(Guid, Guid), CachedReservation> _reservations = new();

        // Capacity: event_id -> CachedCapacity
        private readonly ConcurrentDictionary<Guid, CachedCapacity> _capacities = new();

        // Rate limiting: key -> (count, window_start)
        private readonly ConcurrentDictionary<string, (int count, DateTime windowStart)> _rateLimits = new();

        // Locks
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly object _globalLock = new();

        #endregion

        #region Queue Operations

        public Task QueueAddAsync(Guid eventId, Guid userId, int rank)
        {
            lock (_globalLock)
            {
                if (!_queues.TryGetValue(eventId, out var queue))
                {
                    queue = new SortedDictionary<int, Guid>();
                    _queues[eventId] = queue;
                    _queueReverse[eventId] = new Dictionary<Guid, int>();
                }

                queue[rank] = userId;
                _queueReverse[eventId][userId] = rank;
            }
            return Task.CompletedTask;
        }

        public Task QueueAddBulkAsync(Guid eventId, (Guid userId, int rank)[] entries)
        {
            lock (_globalLock)
            {
                if (!_queues.TryGetValue(eventId, out var queue))
                {
                    queue = new SortedDictionary<int, Guid>();
                    _queues[eventId] = queue;
                    _queueReverse[eventId] = new Dictionary<Guid, int>();
                }

                foreach (var (userId, rank) in entries)
                {
                    queue[rank] = userId;
                    _queueReverse[eventId][userId] = rank;
                }
            }
            return Task.CompletedTask;
        }

        public Task<int?> QueueGetRankAsync(Guid eventId, Guid userId)
        {
            if (_queueReverse.TryGetValue(eventId, out var reverse) &&
                reverse.TryGetValue(userId, out var rank))
            {
                return Task.FromResult<int?>(rank);
            }
            return Task.FromResult<int?>(null);
        }

        public Task<Guid[]> QueueGetRangeByRankAsync(Guid eventId, int startRank, int endRank)
        {
            if (!_queues.TryGetValue(eventId, out var queue))
                return Task.FromResult(Array.Empty<Guid>());

            var result = queue
                .Where(kvp => kvp.Key >= startRank && kvp.Key <= endRank)
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToArray();

            return Task.FromResult(result);
        }

        public Task<bool> QueueRemoveAsync(Guid eventId, Guid userId)
        {
            lock (_globalLock)
            {
                if (!_queueReverse.TryGetValue(eventId, out var reverse) ||
                    !reverse.TryGetValue(userId, out var rank))
                    return Task.FromResult(false);

                reverse.Remove(userId);
                if (_queues.TryGetValue(eventId, out var queue))
                {
                    queue.Remove(rank);
                }
                return Task.FromResult(true);
            }
        }

        public Task<long> QueueCountAsync(Guid eventId)
        {
            if (_queues.TryGetValue(eventId, out var queue))
                return Task.FromResult((long)queue.Count);
            return Task.FromResult(0L);
        }

        public Task<bool> QueueExistsAsync(Guid eventId)
        {
            return Task.FromResult(_queues.ContainsKey(eventId));
        }

        public Task QueueDeleteAsync(Guid eventId)
        {
            lock (_globalLock)
            {
                _queues.TryRemove(eventId, out _);
                _queueReverse.TryRemove(eventId, out _);
            }
            return Task.CompletedTask;
        }

        #endregion

        #region Reservation Operations

        public Task ReservationSetAsync(Guid eventId, Guid userId, CachedReservation reservation)
        {
            _reservations[(eventId, userId)] = reservation;
            return Task.CompletedTask;
        }

        public Task<CachedReservation?> ReservationGetAsync(Guid eventId, Guid userId)
        {
            _reservations.TryGetValue((eventId, userId), out var res);
            return Task.FromResult(res);
        }

        public Task ReservationUpdateStatusAsync(Guid eventId, Guid userId, ReservationStatus status)
        {
            if (_reservations.TryGetValue((eventId, userId), out var res))
            {
                res.Status = status;
            }
            return Task.CompletedTask;
        }

        public Task ReservationDeleteAsync(Guid eventId, Guid userId)
        {
            _reservations.TryRemove((eventId, userId), out _);
            return Task.CompletedTask;
        }

        #endregion

        #region Capacity Operations

        public Task CapacitySetAsync(Guid eventId, CachedCapacity capacity)
        {
            _capacities[eventId] = capacity;
            return Task.CompletedTask;
        }

        public Task<CachedCapacity?> CapacityGetAsync(Guid eventId)
        {
            _capacities.TryGetValue(eventId, out var cap);
            return Task.FromResult(cap);
        }

        public Task<int> CapacityIncrementReservedAsync(Guid eventId, int delta)
        {
            lock (_globalLock)
            {
                if (!_capacities.TryGetValue(eventId, out var cap))
                    return Task.FromResult(-1);

                cap.CapacityReserved += delta;
                return Task.FromResult(cap.CapacityReserved);
            }
        }

        public Task<int> CapacityIncrementConfirmedAsync(Guid eventId, int delta)
        {
            lock (_globalLock)
            {
                if (!_capacities.TryGetValue(eventId, out var cap))
                    return Task.FromResult(-1);

                cap.CapacityConfirmed += delta;
                return Task.FromResult(cap.CapacityConfirmed);
            }
        }

        public Task<int> CapacityDecrementReservedAsync(Guid eventId, int delta)
        {
            lock (_globalLock)
            {
                if (!_capacities.TryGetValue(eventId, out var cap))
                    return Task.FromResult(-1);

                cap.CapacityReserved = Math.Max(0, cap.CapacityReserved - delta);
                return Task.FromResult(cap.CapacityReserved);
            }
        }

        public Task<int> CapacityGetRemainingAsync(Guid eventId)
        {
            if (_capacities.TryGetValue(eventId, out var cap))
                return Task.FromResult(cap.CapacityRemaining);
            return Task.FromResult(0);
        }

        #endregion

        #region Rate Limiting

        public Task<RateLimitResult> CheckRateLimitUserAsync(Guid userId, int maxRequests, TimeSpan window)
        {
            return CheckRateLimitAsync($"user:{userId}", maxRequests, window);
        }

        public Task<RateLimitResult> CheckRateLimitIpAsync(string ipAddress, int maxRequests, TimeSpan window)
        {
            return CheckRateLimitAsync($"ip:{ipAddress}", maxRequests, window);
        }

        private Task<RateLimitResult> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window)
        {
            var now = DateTime.UtcNow;

            lock (_globalLock)
            {
                if (!_rateLimits.TryGetValue(key, out var data) || now - data.windowStart > window)
                {
                    _rateLimits[key] = (1, now);
                    return Task.FromResult(RateLimitResult.Allowed(maxRequests - 1));
                }

                if (data.count >= maxRequests)
                {
                    var retryAfter = window - (now - data.windowStart);
                    return Task.FromResult(RateLimitResult.Blocked(retryAfter));
                }

                _rateLimits[key] = (data.count + 1, data.windowStart);
                return Task.FromResult(RateLimitResult.Allowed(maxRequests - data.count - 1));
            }
        }

        #endregion

        #region Distributed Locking

        public Task<IAsyncDisposable?> AcquireLockAsync(string lockKey, TimeSpan expiry)
        {
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            if (semaphore.Wait(0))
            {
                return Task.FromResult<IAsyncDisposable?>(new LockHandle(semaphore, lockKey, this));
            }

            return Task.FromResult<IAsyncDisposable?>(null);
        }

        private sealed class LockHandle : IAsyncDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly string _key;
            private readonly InMemoryCacheService _cache;

            public LockHandle(SemaphoreSlim semaphore, string key, InMemoryCacheService cache)
            {
                _semaphore = semaphore;
                _key = key;
                _cache = cache;
            }

            public ValueTask DisposeAsync()
            {
                _semaphore.Release();
                return ValueTask.CompletedTask;
            }
        }

        #endregion
    }
}
