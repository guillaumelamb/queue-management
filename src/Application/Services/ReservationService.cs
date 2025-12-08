using System;
using System.Threading.Tasks;
using QueueManagement.Application.DTOs;
using QueueManagement.Domain;
using QueueManagement.Domain.Cache;
using QueueManagement.Domain.Entities;
using QueueManagement.Domain.Repositories;

namespace QueueManagement.Application.Services
{
    /// <summary>
    /// Service for managing reservations and registrations.
    /// Implements the transactional reservation/confirmation logic.
    /// </summary>
    public sealed class ReservationService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IRegistrationRepository _registrationRepository;
        private readonly ICacheService _cacheService;

        public ReservationService(
            IEventRepository eventRepository,
            IQueueEntryRepository queueEntryRepository,
            IReservationRepository reservationRepository,
            IRegistrationRepository registrationRepository,
            ICacheService cacheService)
        {
            _eventRepository = eventRepository;
            _queueEntryRepository = queueEntryRepository;
            _reservationRepository = reservationRepository;
            _registrationRepository = registrationRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Create a reservation for an event slot.
        /// POST /events/{eventId}/reservations
        /// 
        /// Preconditions:
        /// - User must have status = INVITED in queue
        /// - Capacity must be available
        /// - No existing active reservation
        /// </summary>
        public async Task<ServiceResult<ReservationResponse>> CreateReservationAsync(Guid eventId, Guid userId)
        {
            // Acquire distributed lock for this user+event
            var lockKey = $"reservation:{eventId}:{userId}";
            await using var lockHandle = await _cacheService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10));
            if (lockHandle == null)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.Conflict,
                    "Another reservation request is in progress. Please try again.");
            }

            // Validate event
            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.NotFound,
                    "Event not found.");
            }

            if (!evt.IsRegistrationOpen)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.RegistrationClosed,
                    "Registration is not open.");
            }

            // Check queue entry status
            var queueEntry = await _queueEntryRepository.GetByEventAndUserAsync(eventId, userId);
            if (queueEntry == null)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.NotFound,
                    "User is not in the queue.");
            }

            if (queueEntry.Status != QueueEntryStatus.Invited)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.NotInvited,
                    $"User cannot create reservation. Current status: {queueEntry.Status}");
            }

            // Check for existing active reservation
            var existingReservation = await _reservationRepository.GetActiveByEventAndUserAsync(eventId, userId);
            if (existingReservation != null)
            {
                if (existingReservation.Status == ReservationStatus.Confirmed)
                {
                    return ServiceResult<ReservationResponse>.Failure(
                        ErrorCodes.AlreadyRegistered,
                        "User already has a confirmed registration.");
                }

                if (existingReservation.Status == ReservationStatus.Pending && !existingReservation.IsExpired)
                {
                    // Return existing reservation (idempotent)
                    return ServiceResult<ReservationResponse>.Success(new ReservationResponse(
                        ReservationId: existingReservation.Id,
                        EventId: existingReservation.EventId,
                        UserId: existingReservation.UserId,
                        Status: existingReservation.Status.ToString(),
                        ExpiresAt: existingReservation.ExpiresAt,
                        TimeRemaining: existingReservation.TimeRemaining,
                        CreatedAt: existingReservation.CreatedAt
                    ));
                }
            }

            // Check already registered
            if (await _registrationRepository.ExistsAsync(eventId, userId))
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.AlreadyRegistered,
                    "User is already registered for this event.");
            }

            // Check capacity (atomic operation)
            var capacityResult = await _eventRepository.IncrementReservedAsync(eventId, 1);
            if (!capacityResult)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.SoldOut,
                    "No capacity available. Event is sold out.");
            }

            // Update cache
            await _cacheService.CapacityIncrementReservedAsync(eventId, 1);

            // Create reservation
            var reservation = new Reservation
            {
                EventId = eventId,
                UserId = userId,
                QueueEntryId = queueEntry.Id,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddSeconds(evt.ReservationDurationSeconds)
            };

            await _reservationRepository.CreateAsync(reservation);

            // Update cache
            await _cacheService.ReservationSetAsync(eventId, userId, new CachedReservation
            {
                ReservationId = reservation.Id,
                ExpiresAt = reservation.ExpiresAt,
                Status = reservation.Status
            });

            // Update queue entry status
            queueEntry.Status = QueueEntryStatus.Expired; // Move to next state
            await _queueEntryRepository.UpdateAsync(queueEntry);

            return ServiceResult<ReservationResponse>.Success(new ReservationResponse(
                ReservationId: reservation.Id,
                EventId: reservation.EventId,
                UserId: reservation.UserId,
                Status: reservation.Status.ToString(),
                ExpiresAt: reservation.ExpiresAt,
                TimeRemaining: reservation.TimeRemaining,
                CreatedAt: reservation.CreatedAt
            ));
        }

        /// <summary>
        /// Get current reservation for user.
        /// GET /events/{eventId}/reservations/current
        /// </summary>
        public async Task<ServiceResult<ReservationResponse>> GetCurrentReservationAsync(Guid eventId, Guid userId)
        {
            // Try cache first
            var cached = await _cacheService.ReservationGetAsync(eventId, userId);
            if (cached != null)
            {
                var reservation = await _reservationRepository.GetByIdAsync(cached.ReservationId);
                if (reservation != null)
                {
                    return ServiceResult<ReservationResponse>.Success(new ReservationResponse(
                        ReservationId: reservation.Id,
                        EventId: reservation.EventId,
                        UserId: reservation.UserId,
                        Status: reservation.Status.ToString(),
                        ExpiresAt: reservation.ExpiresAt,
                        TimeRemaining: reservation.TimeRemaining,
                        CreatedAt: reservation.CreatedAt
                    ));
                }
            }

            // Fallback to database
            var dbReservation = await _reservationRepository.GetActiveByEventAndUserAsync(eventId, userId);
            if (dbReservation == null)
            {
                return ServiceResult<ReservationResponse>.Failure(
                    ErrorCodes.NotFound,
                    "No active reservation found.");
            }

            return ServiceResult<ReservationResponse>.Success(new ReservationResponse(
                ReservationId: dbReservation.Id,
                EventId: dbReservation.EventId,
                UserId: dbReservation.UserId,
                Status: dbReservation.Status.ToString(),
                ExpiresAt: dbReservation.ExpiresAt,
                TimeRemaining: dbReservation.TimeRemaining,
                CreatedAt: dbReservation.CreatedAt
            ));
        }

        /// <summary>
        /// Confirm a reservation (complete registration).
        /// POST /events/{eventId}/reservations/{reservationId}/confirm
        /// 
        /// Transaction:
        /// 1. Lock reservation
        /// 2. Verify status + expiration
        /// 3. Verify no existing registration
        /// 4. Create registration
        /// 5. Update reservation status
        /// 6. Update cache
        /// </summary>
        public async Task<ServiceResult<RegistrationResponse>> ConfirmReservationAsync(
            Guid eventId, 
            Guid reservationId, 
            Guid userId)
        {
            // Acquire lock on reservation
            var lockKey = $"confirm:{reservationId}";
            await using var lockHandle = await _cacheService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10));
            if (lockHandle == null)
            {
                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.Conflict,
                    "Another confirmation request is in progress. Please try again.");
            }

            // Get and lock reservation
            var reservation = await _reservationRepository.GetAndLockAsync(reservationId);
            if (reservation == null)
            {
                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.NotFound,
                    "Reservation not found.");
            }

            // Validate ownership
            if (reservation.UserId != userId || reservation.EventId != eventId)
            {
                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.Forbidden,
                    "Reservation does not belong to this user.");
            }

            // Validate status
            if (reservation.Status != ReservationStatus.Pending)
            {
                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.InvalidStatus,
                    $"Reservation cannot be confirmed. Current status: {reservation.Status}");
            }

            // Validate not expired
            if (reservation.IsExpired)
            {
                reservation.Status = ReservationStatus.Expired;
                await _reservationRepository.UpdateAsync(reservation);
                await _cacheService.ReservationUpdateStatusAsync(eventId, userId, ReservationStatus.Expired);

                // Release capacity
                await _eventRepository.DecrementReservedAsync(eventId, 1);
                await _cacheService.CapacityDecrementReservedAsync(eventId, 1);

                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.ReservationExpired,
                    "Reservation has expired.");
            }

            // Check for existing registration (should not happen, but safety check)
            if (await _registrationRepository.ExistsAsync(eventId, userId))
            {
                return ServiceResult<RegistrationResponse>.Failure(
                    ErrorCodes.AlreadyRegistered,
                    "User is already registered for this event.");
            }

            // Create registration
            var registration = new Registration
            {
                EventId = eventId,
                UserId = userId,
                ReservationId = reservationId,
                ConfirmedAt = DateTime.UtcNow,
                PaymentStatus = PaymentStatus.NotRequired // Can be extended for payment flow
            };

            await _registrationRepository.CreateAsync(registration);

            // Update reservation status
            reservation.Status = ReservationStatus.Confirmed;
            await _reservationRepository.UpdateAsync(reservation);

            // Update event capacity (move from reserved to confirmed)
            await _eventRepository.DecrementReservedAsync(eventId, 1);
            await _eventRepository.IncrementConfirmedAsync(eventId, 1);

            // Update cache
            await _cacheService.ReservationUpdateStatusAsync(eventId, userId, ReservationStatus.Confirmed);
            await _cacheService.CapacityDecrementReservedAsync(eventId, 1);
            await _cacheService.CapacityIncrementConfirmedAsync(eventId, 1);

            return ServiceResult<RegistrationResponse>.Success(new RegistrationResponse(
                RegistrationId: registration.Id,
                EventId: registration.EventId,
                UserId: registration.UserId,
                ReservationId: registration.ReservationId,
                ConfirmedAt: registration.ConfirmedAt,
                PaymentStatus: registration.PaymentStatus.ToString()
            ));
        }
    }
}
