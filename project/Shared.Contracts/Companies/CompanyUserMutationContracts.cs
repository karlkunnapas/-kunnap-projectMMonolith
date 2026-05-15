namespace Shared.Contracts.Companies;

public sealed class CompanyUserMutationResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public CompanyMembershipContract? Membership { get; set; }
}

