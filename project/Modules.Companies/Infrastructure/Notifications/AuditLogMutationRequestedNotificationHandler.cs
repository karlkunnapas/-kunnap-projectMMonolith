using Mediator;
using Microsoft.EntityFrameworkCore;
using Modules.Companies.Domain;
using Modules.Companies.Infrastructure;
using Shared.Contracts.Companies.Events;

namespace Modules.Companies.Infrastructure.Notifications;

internal sealed class AuditLogMutationRequestedNotificationHandler
    : INotificationHandler<AuditLogMutationRequestedNotification>
{
    private readonly CompaniesDbContext _dbContext;

    public AuditLogMutationRequestedNotificationHandler(CompaniesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask Handle(AuditLogMutationRequestedNotification notification, CancellationToken cancellationToken)
    {
        // CompanyId is required in current model. Skip unresolved/system-wide events.
        if (notification.CompanyId == Guid.Empty)
        {
            return;
        }

        var companyExists = await _dbContext.Companies
            .AnyAsync(c => c.Id == notification.CompanyId, cancellationToken);
        if (!companyExists)
        {
            return;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            UserName = string.IsNullOrWhiteSpace(notification.UserName) ? "system" : notification.UserName.Trim(),
            EntityName = notification.EntityName.Trim(),
            EntityId = notification.EntityId,
            Action = notification.Action.Trim(),
            AtUtc = notification.AtUtc,
            ChangesJson = notification.ChangesJson
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
