namespace QueueManagement.Domain
{
    /// <summary>
    /// Status of a queue entry.
    /// </summary>
    public enum QueueEntryStatus
    {
        /// <summary>Waiting in queue, not yet invited.</summary>
        Pending,
        
        /// <summary>Invited to create a reservation.</summary>
        Invited,
        
        /// <summary>Invitation expired without action.</summary>
        Expired,
        
        /// <summary>Skipped (e.g., user cancelled).</summary>
        Skipped
    }

    /// <summary>
    /// Status of a reservation.
    /// </summary>
    public enum ReservationStatus
    {
        /// <summary>Reservation created, awaiting confirmation.</summary>
        Pending,
        
        /// <summary>Reservation confirmed (registration complete).</summary>
        Confirmed,
        
        /// <summary>Reservation cancelled by user.</summary>
        Cancelled,
        
        /// <summary>Reservation expired (timeout).</summary>
        Expired
    }

    /// <summary>
    /// Payment status for registrations requiring payment.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>No payment required for this event.</summary>
        NotRequired,
        
        /// <summary>Payment pending.</summary>
        Pending,
        
        /// <summary>Payment completed.</summary>
        Paid,
        
        /// <summary>Payment failed.</summary>
        Failed
    }

    /// <summary>
    /// API error codes for consistent error handling.
    /// </summary>
    public static class ErrorCodes
    {
        public const string BadRequest = "BAD_REQUEST";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string SoldOut = "SOLD_OUT";
        public const string ReservationExpired = "RESERVATION_EXPIRED";
        public const string InvalidStatus = "INVALID_STATUS";
        public const string AlreadyRegistered = "ALREADY_REGISTERED";
        public const string AlreadyPreRegistered = "ALREADY_PRE_REGISTERED";
        public const string TooManyRequests = "TOO_MANY_REQUESTS";
        public const string RegistrationNotOpen = "REGISTRATION_NOT_OPEN";
        public const string RegistrationClosed = "REGISTRATION_CLOSED";
        public const string NotInvited = "NOT_INVITED";
    }
}
