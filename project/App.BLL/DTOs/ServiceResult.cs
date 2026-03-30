namespace App.BLL.DTOs;

/// <summary>
/// Standard service result wrapper that indicates success/failure and contains data or errors.
/// Used throughout the service layer to communicate operation outcomes without throwing exceptions.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public List<ServiceError> Errors { get; set; } = new();

    /// <summary>
    /// Creates a successful result with the specified data.
    /// </summary>
    public static ServiceResult<T> Ok(T data)
    {
        return new ServiceResult<T> { Success = true, Data = data };
    }

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static ServiceResult<T> Fail(string code, string message)
    {
        return new ServiceResult<T> 
        { 
            Success = false, 
            Errors = new List<ServiceError> { new() { Code = code, Message = message } }
        };
    }

    /// <summary>
    /// Creates a failed result with multiple errors.
    /// </summary>
    public static ServiceResult<T> Fail(List<ServiceError> errors)
    {
        return new ServiceResult<T> { Success = false, Errors = errors };
    }
}

/// <summary>
/// Non-generic version for operations that don't return data.
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public List<ServiceError> Errors { get; set; } = new();

    public static ServiceResult Ok()
    {
        return new ServiceResult { Success = true };
    }

    public static ServiceResult Fail(string code, string message)
    {
        return new ServiceResult 
        { 
            Success = false, 
            Errors = new List<ServiceError> { new() { Code = code, Message = message } }
        };
    }

    public static ServiceResult Fail(List<ServiceError> errors)
    {
        return new ServiceResult { Success = false, Errors = errors };
    }
}

/// <summary>
/// Represents a single error in a service operation.
/// </summary>
public class ServiceError
{
    /// <summary>
    /// Error code for programmatic handling (e.g., "COMPANY_NOT_FOUND").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}