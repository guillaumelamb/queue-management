using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QueueManagement.Domain.Entities;

namespace QueueManagement.Domain.Repositories
{
    /// <summary>
    /// Repository for User entities.
    /// </summary>
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
    }

    /// <summary>
    /// Repository for Event entities.
    /// </summary>
    public interface IEventRepository
    {
        Task<Event?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<Event>> GetOpenEventsAsync();
        Task<Event> CreateAsync(Event evt);
        Task<Event> UpdateAsync(Event evt);
        
        /// <summary>
        /// Atomically update capacity counters.
        /// </summary>
        Task<bool> IncrementReservedAsync(Guid eventId, int delta);
        Task<bool> IncrementConfirmedAsync(Guid eventId, int delta);
        Task<bool> DecrementReservedAsync(Guid eventId, int delta);
    }

    /// <summary>
    /// Repository for PreRegistration entities.
    /// </summary>
    public interface IPreRegistrationRepository
    {
        Task<PreRegistration?> GetByIdAsync(Guid id);
        Task<PreRegistration?> GetByEventAndUserAsync(Guid eventId, Guid userId);
        Task<IReadOnlyList<PreRegistration>> GetByEventAsync(Guid eventId);
        Task<int> CountByEventAsync(Guid eventId);
        
        /// <summary>
        /// Create a pre-registration. Returns existing if duplicate.
        /// </summary>
        Task<(PreRegistration registration, bool created)> CreateOrGetAsync(PreRegistration preReg);
    }

    /// <summary>
    /// Repository for QueueEntry entities.
    /// </summary>
    public interface IQueueEntryRepository
    {
        Task<QueueEntry?> GetByIdAsync(Guid id);
        Task<QueueEntry?> GetByEventAndUserAsync(Guid eventId, Guid userId);
        Task<QueueEntry?> GetByEventAndRankAsync(Guid eventId, int rank);
        Task<IReadOnlyList<QueueEntry>> GetByEventAsync(Guid eventId);
        Task<IReadOnlyList<QueueEntry>> GetByEventAndStatusAsync(Guid eventId, QueueEntryStatus status);
        
        /// <summary>
        /// Get entries with rank less than or equal to specified value.
        /// </summary>
        Task<IReadOnlyList<QueueEntry>> GetInvitableEntriesAsync(Guid eventId, int maxRank);
        
        Task<QueueEntry> CreateAsync(QueueEntry entry);
        Task CreateBulkAsync(IEnumerable<QueueEntry> entries);
        Task<QueueEntry> UpdateAsync(QueueEntry entry);
        Task UpdateStatusAsync(Guid id, QueueEntryStatus status);
        
        /// <summary>
        /// Check if ranks have been computed for an event.
        /// </summary>
        Task<bool> HasRanksAsync(Guid eventId);
    }

    /// <summary>
    /// Repository for Reservation entities.
    /// </summary>
    public interface IReservationRepository
    {
        Task<Reservation?> GetByIdAsync(Guid id);
        Task<Reservation?> GetByEventAndUserAsync(Guid eventId, Guid userId);
        Task<Reservation?> GetActiveByEventAndUserAsync(Guid eventId, Guid userId);
        Task<IReadOnlyList<Reservation>> GetExpiredPendingAsync(DateTime asOf);
        Task<int> CountPendingByEventAsync(Guid eventId);
        
        Task<Reservation> CreateAsync(Reservation reservation);
        Task<Reservation> UpdateAsync(Reservation reservation);
        Task UpdateStatusAsync(Guid id, ReservationStatus status);
        
        /// <summary>
        /// Lock and get reservation for update (pessimistic locking simulation).
        /// </summary>
        Task<Reservation?> GetAndLockAsync(Guid id);
    }

    /// <summary>
    /// Repository for Registration entities.
    /// </summary>
    public interface IRegistrationRepository
    {
        Task<Registration?> GetByIdAsync(Guid id);
        Task<Registration?> GetByEventAndUserAsync(Guid eventId, Guid userId);
        Task<Registration?> GetByReservationAsync(Guid reservationId);
        Task<int> CountByEventAsync(Guid eventId);
        
        Task<Registration> CreateAsync(Registration registration);
        Task<Registration> UpdateAsync(Registration registration);
        
        /// <summary>
        /// Check if user is already registered for event.
        /// </summary>
        Task<bool> ExistsAsync(Guid eventId, Guid userId);
    }
}
