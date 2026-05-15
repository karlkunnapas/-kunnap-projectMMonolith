namespace Shared.Contracts.Companies;

public sealed class AddCompanyUserContract
{
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
    public string Role { get; set; } = string.Empty;
}

public sealed class AddCompanyUserResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsExistingUser { get; set; }
    public string AccessStatus { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
}

