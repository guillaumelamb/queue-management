using System;

namespace QueueManagement.Application.DTOs
{
    #region Request DTOs

    public sealed record CreatePreRegistrationRequest(Guid EventId);

    public sealed record CreateReservationRequest(Guid EventId);

    public sealed record ConfirmReservationRequest(Guid EventId, Guid ReservationId);

    #endregion

    #region Response DTOs

    public sealed record PreRegistrationResponse(
        Guid Id,
        Guid EventId,
        Guid UserId,
        DateTime CreatedAt,
        bool WasExisting
    );

    public sealed record QueueStatusResponse(
        Guid EventId,
        Guid UserId,
        int? Rank,
        string Status,
        DateTime? WaveStartAt,
        bool CanCreateReservation,
        int TotalInQueue,
        int CurrentPosition,
        TimeSpan? EstimatedWait
    );

    public sealed record ReservationResponse(
        Guid ReservationId,
        Guid EventId,
        Guid UserId,
        string Status,
        DateTime ExpiresAt,
        TimeSpan? TimeRemaining,
        DateTime CreatedAt
    );

    public sealed record RegistrationResponse(
        Guid RegistrationId,
        Guid EventId,
        Guid UserId,
        Guid ReservationId,
        DateTime ConfirmedAt,
        string PaymentStatus
    );

    public sealed record EventCapacityResponse(
        Guid EventId,
        int CapacityTotal,
        int CapacityReserved,
        int CapacityConfirmed,
        int CapacityRemaining,
        bool IsOpen
    );

    public sealed record ErrorResponse(
        string Code,
        string Message,
        string? Detail = null
    );

    #endregion

    #region Service Results

    public abstract record ServiceResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed record ServiceResult<T> : ServiceResult
    {
        public T? Data { get; init; }

        public static ServiceResult<T> Success(T data) => new()
        {
            IsSuccess = true,
            Data = data
        };

        public static ServiceResult<T> Failure(string errorCode, string message) => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message
        };
    }

    #endregion
}
