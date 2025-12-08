using System;
using System.Threading.Tasks;

namespace QueueManagement.Domain.Cache
{
    /// <summary>
    /// Cache entry for reservation data in Redis.
    /// Key pattern: reservation:{event_id}:{user_id}
    /// </summary>
    public sealed class CachedReservation
    {
        public Guid ReservationId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public ReservationStatus Status { get; set; }
    }

    /// <summary>
    /// Cache entry for event capacity in Redis.
    /// Key pattern: capacity:{event_id}
    /// </summary>
    public sealed class CachedCapacity
    {
        public int CapacityTotal { get; set; }
        public int CapacityReserved { get; set; }
        public int CapacityConfirmed { get; set; }
        public int CapacityRemaining => CapacityTotal - CapacityReserved - CapacityConfirmed;
    }

    /// <summary>
    /// Represents a rate limit check result.
    /// </summary>
    public sealed class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan? RetryAfter { get; set; }

        public static RateLimitResult Allowed(int remaining) => new() { IsAllowed = true, RemainingRequests = remaining };
        public static RateLimitResult Blocked(TimeSpan retryAfter) => new() { IsAllowed = false, RetryAfter = retryAfter };
    }

    /// <summary>
    /// Interface for Redis cache operations.
    /// Abstracts Redis commands for queue management.
    /// </summary>
    public interface ICacheService
    {
        #region Queue Operations (ZSET)

        /// <summary>
        /// Add a user to the event queue with a specific rank.
        /// Key: queue:{eventId}
        /// </summary>
        Task QueueAddAsync(Guid eventId, Guid userId, int rank);

        /// <summary>
        /// Add multiple users to the queue (bulk operation).
        /// </summary>
        Task QueueAddBulkAsync(Guid eventId, (Guid userId, int rank)[] entries);

        /// <summary>
        /// Get user's rank in the queue.
        /// </summary>
        Task<int?> QueueGetRankAsync(Guid eventId, Guid userId);

        /// <summary>
        /// Get users within a rank range.
        /// </summary>
        Task<Guid[]> QueueGetRangeByRankAsync(Guid eventId, int startRank, int endRank);

        /// <summary>
        /// Remove user from queue.
        /// </summary>
        Task<bool> QueueRemoveAsync(Guid eventId, Guid userId);

        /// <summary>
        /// Get total queue size.
        /// </summary>
        Task<long> QueueCountAsync(Guid eventId);

        /// <summary>
        /// Check if queue exists for event.
        /// </summary>
        Task<bool> QueueExistsAsync(Guid eventId);

        /// <summary>
        /// Delete entire queue (for rebuild).
        /// </summary>
        Task QueueDeleteAsync(Guid eventId);

        #endregion

        #region Reservation Operations (HASH)

        /// <summary>
        /// Set reservation data with TTL.
        /// Key: reservation:{eventId}:{userId}
        /// </summary>
        Task ReservationSetAsync(Guid eventId, Guid userId, CachedReservation reservation);

        /// <summary>
        /// Get reservation data.
        /// </summary>
        Task<CachedReservation?> ReservationGetAsync(Guid eventId, Guid userId);

        /// <summary>
        /// Update reservation status.
        /// </summary>
        Task ReservationUpdateStatusAsync(Guid eventId, Guid userId, ReservationStatus status);

        /// <summary>
        /// Delete reservation.
        /// </summary>
        Task ReservationDeleteAsync(Guid eventId, Guid userId);

        #endregion

        #region Capacity Operations (HASH)

        /// <summary>
        /// Set capacity data for event.
        /// Key: capacity:{eventId}
        /// </summary>
        Task CapacitySetAsync(Guid eventId, CachedCapacity capacity);

        /// <summary>
        /// Get capacity data for event.
        /// </summary>
        Task<CachedCapacity?> CapacityGetAsync(Guid eventId);

        /// <summary>
        /// Atomically increment reserved count.
        /// </summary>
        Task<int> CapacityIncrementReservedAsync(Guid eventId, int delta);

        /// <summary>
        /// Atomically increment confirmed count.
        /// </summary>
        Task<int> CapacityIncrementConfirmedAsync(Guid eventId, int delta);

        /// <summary>
        /// Atomically decrement reserved count.
        /// </summary>
        Task<int> CapacityDecrementReservedAsync(Guid eventId, int delta);

        /// <summary>
        /// Check remaining capacity (atomic read).
        /// </summary>
        Task<int> CapacityGetRemainingAsync(Guid eventId);

        #endregion

        #region Rate Limiting

        /// <summary>
        /// Check and increment rate limit for user.
        /// Key: ratelimit:user:{userId}
        /// </summary>
        Task<RateLimitResult> CheckRateLimitUserAsync(Guid userId, int maxRequests, TimeSpan window);

        /// <summary>
        /// Check and increment rate limit for IP.
        /// Key: ratelimit:ip:{ip}
        /// </summary>
        Task<RateLimitResult> CheckRateLimitIpAsync(string ipAddress, int maxRequests, TimeSpan window);

        #endregion

        #region Distributed Locking

        /// <summary>
        /// Acquire a distributed lock.
        /// </summary>
        Task<IAsyncDisposable?> AcquireLockAsync(string lockKey, TimeSpan expiry);

        #endregion
    }
}
