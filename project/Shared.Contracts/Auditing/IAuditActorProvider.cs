namespace Shared.Contracts.Auditing;

public interface IAuditActorProvider
{
    string? UserName { get; }
}
