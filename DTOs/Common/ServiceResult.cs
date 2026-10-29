namespace tran_nguyen_vi_khang_api.DTOs.Common;

public enum ServiceErrorType
{
    None,
    NotFound,
    BadRequest,
    Forbidden,
    Conflict,
    InternalError
}

public class ServiceResult<T>
{
    public bool IsSuccess => ErrorType == ServiceErrorType.None;
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public ServiceErrorType ErrorType { get; set; } = ServiceErrorType.None;

    public static ServiceResult<T> Success(T data) => new() { Data = data };
    public static ServiceResult<T> NotFound(string message) => new() { ErrorType = ServiceErrorType.NotFound, ErrorMessage = message };
    public static ServiceResult<T> BadRequest(string message) => new() { ErrorType = ServiceErrorType.BadRequest, ErrorMessage = message };
    public static ServiceResult<T> Forbidden(string message) => new() { ErrorType = ServiceErrorType.Forbidden, ErrorMessage = message };
    public static ServiceResult<T> Conflict(string message) => new() { ErrorType = ServiceErrorType.Conflict, ErrorMessage = message };
    public static ServiceResult<T> InternalError(string message) => new() { ErrorType = ServiceErrorType.InternalError, ErrorMessage = message };
}

