namespace Shared.Contracts.Users;

public sealed class RegisterCustomerContract
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterCustomerResultContract
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static RegisterCustomerResultContract Ok(Guid userId) => new()
    {
        Success = true,
        UserId = userId
    };

    public static RegisterCustomerResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
