using System;
using System.Threading.Tasks;
using QueueManagement.Application.DTOs;
using QueueManagement.Application.Services;
using QueueManagement.Domain;

namespace QueueManagement.Api.Controllers
{
    /// <summary>
    /// Simulates REST API controller behavior.
    /// In a real ASP.NET Core application, these would be actual controllers.
    /// </summary>
    public sealed class ApiResponse<T>
    {
        public int StatusCode { get; init; }
        public T? Data { get; init; }
        public ErrorResponse? Error { get; init; }
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

        public static ApiResponse<T> Ok(T data) => new() { StatusCode = 200, Data = data };
        public static ApiResponse<T> Created(T data) => new() { StatusCode = 201, Data = data };
        public static ApiResponse<T> BadRequest(string code, string message) => new() 
        { 
            StatusCode = 400, 
            Error = new ErrorResponse(code, message) 
        };
        public static ApiResponse<T> Unauthorized(string message) => new() 
        { 
            StatusCode = 401, 
            Error = new ErrorResponse(ErrorCodes.Unauthorized, message) 
        };
        public static ApiResponse<T> Forbidden(string message) => new() 
        { 
            StatusCode = 403, 
            Error = new ErrorResponse(ErrorCodes.Forbidden, message) 
        };
        public static ApiResponse<T> NotFound(string message) => new() 
        { 
            StatusCode = 404, 
            Error = new ErrorResponse(ErrorCodes.NotFound, message) 
        };
        public static ApiResponse<T> Conflict(string code, string message) => new() 
        { 
            StatusCode = 409, 
            Error = new ErrorResponse(code, message) 
        };
        public static ApiResponse<T> TooManyRequests(string message) => new() 
        { 
            StatusCode = 429, 
            Error = new ErrorResponse(ErrorCodes.TooManyRequests, message) 
        };
    }

    /// <summary>
    /// Controller for pre-registration endpoints.
    /// </summary>
    public sealed class PreRegistrationController
    {
        private readonly QueueService _queueService;

        public PreRegistrationController(QueueService queueService)
        {
            _queueService = queueService;
        }

        /// <summary>
        /// POST /events/{eventId}/pre-registrations
        /// Create a pre-registration for an event.
        /// </summary>
        public async Task<ApiResponse<PreRegistrationResponse>> CreateAsync(
            Guid eventId, 
            Guid userId, 
            string? ipAddress = null)
        {
            var result = await _queueService.CreatePreRegistrationAsync(eventId, userId, ipAddress);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<PreRegistrationResponse>.NotFound(result.ErrorMessage!),
                    ErrorCodes.TooManyRequests => ApiResponse<PreRegistrationResponse>.TooManyRequests(result.ErrorMessage!),
                    ErrorCodes.RegistrationNotOpen => ApiResponse<PreRegistrationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.RegistrationClosed => ApiResponse<PreRegistrationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.AlreadyRegistered => ApiResponse<PreRegistrationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    _ => ApiResponse<PreRegistrationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return result.Data!.WasExisting 
                ? ApiResponse<PreRegistrationResponse>.Ok(result.Data)
                : ApiResponse<PreRegistrationResponse>.Created(result.Data);
        }
    }

    /// <summary>
    /// Controller for queue status endpoints.
    /// </summary>
    public sealed class QueueController
    {
        private readonly QueueService _queueService;

        public QueueController(QueueService queueService)
        {
            _queueService = queueService;
        }

        /// <summary>
        /// GET /events/{eventId}/queue/me
        /// Get current user's queue status.
        /// </summary>
        public async Task<ApiResponse<QueueStatusResponse>> GetMyStatusAsync(Guid eventId, Guid userId)
        {
            var result = await _queueService.GetQueueStatusAsync(eventId, userId);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<QueueStatusResponse>.NotFound(result.ErrorMessage!),
                    _ => ApiResponse<QueueStatusResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return ApiResponse<QueueStatusResponse>.Ok(result.Data!);
        }

        /// <summary>
        /// GET /events/{eventId}/capacity
        /// Get event capacity information.
        /// </summary>
        public async Task<ApiResponse<EventCapacityResponse>> GetCapacityAsync(Guid eventId)
        {
            var result = await _queueService.GetEventCapacityAsync(eventId);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<EventCapacityResponse>.NotFound(result.ErrorMessage!),
                    _ => ApiResponse<EventCapacityResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return ApiResponse<EventCapacityResponse>.Ok(result.Data!);
        }
    }

    /// <summary>
    /// Controller for reservation endpoints.
    /// </summary>
    public sealed class ReservationController
    {
        private readonly ReservationService _reservationService;

        public ReservationController(ReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        /// <summary>
        /// POST /events/{eventId}/reservations
        /// Create a reservation.
        /// </summary>
        public async Task<ApiResponse<ReservationResponse>> CreateAsync(Guid eventId, Guid userId)
        {
            var result = await _reservationService.CreateReservationAsync(eventId, userId);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<ReservationResponse>.NotFound(result.ErrorMessage!),
                    ErrorCodes.Forbidden => ApiResponse<ReservationResponse>.Forbidden(result.ErrorMessage!),
                    ErrorCodes.NotInvited => ApiResponse<ReservationResponse>.Forbidden(result.ErrorMessage!),
                    ErrorCodes.SoldOut => ApiResponse<ReservationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.AlreadyRegistered => ApiResponse<ReservationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.Conflict => ApiResponse<ReservationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    _ => ApiResponse<ReservationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return ApiResponse<ReservationResponse>.Created(result.Data!);
        }

        /// <summary>
        /// GET /events/{eventId}/reservations/current
        /// Get current reservation.
        /// </summary>
        public async Task<ApiResponse<ReservationResponse>> GetCurrentAsync(Guid eventId, Guid userId)
        {
            var result = await _reservationService.GetCurrentReservationAsync(eventId, userId);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<ReservationResponse>.NotFound(result.ErrorMessage!),
                    _ => ApiResponse<ReservationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return ApiResponse<ReservationResponse>.Ok(result.Data!);
        }

        /// <summary>
        /// POST /events/{eventId}/reservations/{reservationId}/confirm
        /// Confirm a reservation.
        /// </summary>
        public async Task<ApiResponse<RegistrationResponse>> ConfirmAsync(
            Guid eventId, 
            Guid reservationId, 
            Guid userId)
        {
            var result = await _reservationService.ConfirmReservationAsync(eventId, reservationId, userId);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.NotFound => ApiResponse<RegistrationResponse>.NotFound(result.ErrorMessage!),
                    ErrorCodes.Forbidden => ApiResponse<RegistrationResponse>.Forbidden(result.ErrorMessage!),
                    ErrorCodes.InvalidStatus => ApiResponse<RegistrationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.ReservationExpired => ApiResponse<RegistrationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.AlreadyRegistered => ApiResponse<RegistrationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    ErrorCodes.Conflict => ApiResponse<RegistrationResponse>.Conflict(result.ErrorCode!, result.ErrorMessage!),
                    _ => ApiResponse<RegistrationResponse>.BadRequest(result.ErrorCode!, result.ErrorMessage!)
                };
            }

            return ApiResponse<RegistrationResponse>.Created(result.Data!);
        }
    }
}
