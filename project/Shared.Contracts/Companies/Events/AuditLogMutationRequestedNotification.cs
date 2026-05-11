using Mediator;

namespace Shared.Contracts.Companies.Events;

public sealed class AuditLogMutationRequestedNotification : INotification
{
    public Guid CompanyId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ChangesJson { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
