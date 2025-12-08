using System;

namespace QueueManagement.Domain.Entities
{
    /// <summary>
    /// Represents a user in the system.
    /// Table: users
    /// </summary>
    public sealed class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? ExternalId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public User(string email, string? externalId = null) : this()
        {
            Email = email;
            ExternalId = externalId;
        }
    }

    /// <summary>
    /// Represents an event with limited capacity.
    /// Table: events
    /// </summary>
    public sealed class Event
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Total available capacity for the event.</summary>
        public int CapacityTotal { get; set; }
        
        /// <summary>Currently reserved (pending confirmation).</summary>
        public int CapacityReserved { get; set; }
        
        /// <summary>Confirmed registrations.</summary>
        public int CapacityConfirmed { get; set; }
        
        /// <summary>When registration opens.</summary>
        public DateTime RegistrationStartAt { get; set; }
        
        /// <summary>When registration closes.</summary>
        public DateTime RegistrationEndAt { get; set; }
        
        /// <summary>Duration in seconds for reservation timeout (e.g., 420 = 7 minutes).</summary>
        public int ReservationDurationSeconds { get; set; } = 420;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>Computed remaining capacity.</summary>
        public int CapacityRemaining => CapacityTotal - CapacityReserved - CapacityConfirmed;

        /// <summary>Check if registration is currently open.</summary>
        public bool IsRegistrationOpen => DateTime.UtcNow >= RegistrationStartAt && DateTime.UtcNow <= RegistrationEndAt;

        public Event()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a user's pre-registration for an event.
    /// This is the entry ticket for the lottery/queue.
    /// Table: pre_registrations
    /// Constraints: UNIQUE(event_id, user_id)
    /// </summary>
    public sealed class PreRegistration
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public PreRegistration()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public PreRegistration(Guid eventId, Guid userId) : this()
        {
            EventId = eventId;
            UserId = userId;
        }
    }

    /// <summary>
    /// Represents a user's position in the queue after lottery.
    /// Table: queue_entries
    /// Constraints: UNIQUE(event_id, user_id), UNIQUE(event_id, rank)
    /// </summary>
    public sealed class QueueEntry
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        
        /// <summary>Deterministic rank in queue (1-based).</summary>
        public int Rank { get; set; }
        
        /// <summary>Optional wave start time for batch processing.</summary>
        public DateTime? WaveStartAt { get; set; }
        
        public QueueEntryStatus Status { get; set; } = QueueEntryStatus.Pending;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public QueueEntry()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a time-limited reservation for an event slot.
    /// Table: reservations
    /// Constraints: UNIQUE(event_id, user_id)
    /// </summary>
    public sealed class Reservation
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public Guid QueueEntryId { get; set; }
        
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        
        /// <summary>When the reservation expires if not confirmed.</summary>
        public DateTime ExpiresAt { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Reservation()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Check if reservation has expired.</summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status == ReservationStatus.Pending;

        /// <summary>Remaining time before expiration.</summary>
        public TimeSpan? TimeRemaining => Status == ReservationStatus.Pending 
            ? ExpiresAt - DateTime.UtcNow 
            : null;
    }

    /// <summary>
    /// Represents a confirmed registration for an event.
    /// Table: registrations
    /// Constraints: UNIQUE(event_id, user_id), UNIQUE(reservation_id)
    /// </summary>
    public sealed class Registration
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public Guid ReservationId { get; set; }
        
        public DateTime ConfirmedAt { get; set; }
        
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Registration()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            ConfirmedAt = DateTime.UtcNow;
        }
    }
}
