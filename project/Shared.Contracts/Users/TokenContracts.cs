namespace Shared.Contracts.Users;

public sealed class IssueRefreshTokenContract
{
    public Guid UserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
