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
    /// Service for managing pre-registrations and queue operations.
    /// Implements the core queue management logic.
    /// </summary>
    public sealed class QueueService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IPreRegistrationRepository _preRegistrationRepository;
        private readonly IQueueEntryRepository _queueEntryRepository;
        private readonly IRegistrationRepository _registrationRepository;
        private readonly ICacheService _cacheService;

        public QueueService(
            IEventRepository eventRepository,
            IPreRegistrationRepository preRegistrationRepository,
            IQueueEntryRepository queueEntryRepository,
            IRegistrationRepository registrationRepository,
            ICacheService cacheService)
        {
            _eventRepository = eventRepository;
            _preRegistrationRepository = preRegistrationRepository;
            _queueEntryRepository = queueEntryRepository;
            _registrationRepository = registrationRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Create a pre-registration for an event.
        /// POST /events/{eventId}/pre-registrations
        /// Idempotent: returns existing if duplicate.
        /// </summary>
        public async Task<ServiceResult<PreRegistrationResponse>> CreatePreRegistrationAsync(
            Guid eventId, 
            Guid userId,
            string? ipAddress = null)
        {
            // Rate limiting
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var rateLimitResult = await _cacheService.CheckRateLimitIpAsync(ipAddress, 10, TimeSpan.FromMinutes(1));
                if (!rateLimitResult.IsAllowed)
                {
                    return ServiceResult<PreRegistrationResponse>.Failure(
                        ErrorCodes.TooManyRequests,
                        $"Rate limit exceeded. Retry after {rateLimitResult.RetryAfter?.TotalSeconds:F0} seconds.");
                }
            }

            var userRateLimit = await _cacheService.CheckRateLimitUserAsync(userId, 5, TimeSpan.FromMinutes(1));
            if (!userRateLimit.IsAllowed)
            {
                return ServiceResult<PreRegistrationResponse>.Failure(
                    ErrorCodes.TooManyRequests,
                    $"Rate limit exceeded. Retry after {userRateLimit.RetryAfter?.TotalSeconds:F0} seconds.");
            }

            // Validate event exists and registration is open
            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return ServiceResult<PreRegistrationResponse>.Failure(
                    ErrorCodes.NotFound,
                    "Event not found.");
            }

            var now = DateTime.UtcNow;
            if (now < evt.RegistrationStartAt)
            {
                return ServiceResult<PreRegistrationResponse>.Failure(
                    ErrorCodes.RegistrationNotOpen,
                    $"Registration opens at {evt.RegistrationStartAt:u}.");
            }

            if (now > evt.RegistrationEndAt)
            {
                return ServiceResult<PreRegistrationResponse>.Failure(
                    ErrorCodes.RegistrationClosed,
                    "Registration period has ended.");
            }

            // Check if already registered
            var existingRegistration = await _registrationRepository.GetByEventAndUserAsync(eventId, userId);
            if (existingRegistration != null)
            {
                return ServiceResult<PreRegistrationResponse>.Failure(
                    ErrorCodes.AlreadyRegistered,
                    "User is already registered for this event.");
            }

            // Create or get existing pre-registration (idempotent)
            var preReg = new PreRegistration(eventId, userId);
            var (registration, created) = await _preRegistrationRepository.CreateOrGetAsync(preReg);

            return ServiceResult<PreRegistrationResponse>.Success(new PreRegistrationResponse(
                Id: registration.Id,
                EventId: registration.EventId,
                UserId: registration.UserId,
                CreatedAt: registration.CreatedAt,
                WasExisting: !created
            ));
        }

        /// <summary>
        /// Get queue status for a user.
        /// GET /events/{eventId}/queue/me
        /// </summary>
        public async Task<ServiceResult<QueueStatusResponse>> GetQueueStatusAsync(Guid eventId, Guid userId)
        {
            // Check if event exists
            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return ServiceResult<QueueStatusResponse>.Failure(
                    ErrorCodes.NotFound,
                    "Event not found.");
            }

            // Check if user has pre-registered
            var preReg = await _preRegistrationRepository.GetByEventAndUserAsync(eventId, userId);
            if (preReg == null)
            {
                return ServiceResult<QueueStatusResponse>.Failure(
                    ErrorCodes.NotFound,
                    "User has not pre-registered for this event.");
            }

            // Get queue entry (after lottery)
            var queueEntry = await _queueEntryRepository.GetByEventAndUserAsync(eventId, userId);
            
            // Try cache first for rank
            int? rank = null;
            if (queueEntry != null)
            {
                rank = await _cacheService.QueueGetRankAsync(eventId, userId) ?? queueEntry.Rank;
            }

            var totalInQueue = await _cacheService.QueueCountAsync(eventId);
            if (totalInQueue == 0)
            {
                totalInQueue = await _preRegistrationRepository.CountByEventAsync(eventId);
            }

            // Determine if user can create reservation
            bool canCreateReservation = queueEntry?.Status == QueueEntryStatus.Invited;

            // Calculate estimated wait time
            TimeSpan? estimatedWait = null;
            if (rank.HasValue && queueEntry?.Status == QueueEntryStatus.Pending)
            {
                // Estimate based on capacity and average processing time
                int capacityRemaining = await _cacheService.CapacityGetRemainingAsync(eventId);
                if (capacityRemaining > 0 && rank.Value > 0)
                {
                    // Rough estimate: 30 seconds per registration
                    estimatedWait = TimeSpan.FromSeconds(Math.Max(0, rank.Value) * 30);
                }
            }

            return ServiceResult<QueueStatusResponse>.Success(new QueueStatusResponse(
                EventId: eventId,
                UserId: userId,
                Rank: rank,
                Status: queueEntry?.Status.ToString() ?? "PENDING_LOTTERY",
                WaveStartAt: queueEntry?.WaveStartAt,
                CanCreateReservation: canCreateReservation,
                TotalInQueue: (int)totalInQueue,
                CurrentPosition: rank ?? 0,
                EstimatedWait: estimatedWait
            ));
        }

        /// <summary>
        /// Get event capacity information.
        /// </summary>
        public async Task<ServiceResult<EventCapacityResponse>> GetEventCapacityAsync(Guid eventId)
        {
            var evt = await _eventRepository.GetByIdAsync(eventId);
            if (evt == null)
            {
                return ServiceResult<EventCapacityResponse>.Failure(
                    ErrorCodes.NotFound,
                    "Event not found.");
            }

            // Try cache first
            var cached = await _cacheService.CapacityGetAsync(eventId);
            if (cached != null)
            {
                return ServiceResult<EventCapacityResponse>.Success(new EventCapacityResponse(
                    EventId: eventId,
                    CapacityTotal: cached.CapacityTotal,
                    CapacityReserved: cached.CapacityReserved,
                    CapacityConfirmed: cached.CapacityConfirmed,
                    CapacityRemaining: cached.CapacityRemaining,
                    IsOpen: evt.IsRegistrationOpen
                ));
            }

            return ServiceResult<EventCapacityResponse>.Success(new EventCapacityResponse(
                EventId: eventId,
                CapacityTotal: evt.CapacityTotal,
                CapacityReserved: evt.CapacityReserved,
                CapacityConfirmed: evt.CapacityConfirmed,
                CapacityRemaining: evt.CapacityRemaining,
                IsOpen: evt.IsRegistrationOpen
            ));
        }
    }
}
