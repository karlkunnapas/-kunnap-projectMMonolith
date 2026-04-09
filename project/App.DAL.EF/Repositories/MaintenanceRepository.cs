using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly AppDbContext _context;

    public MaintenanceRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Maintenance?> GetByIdAsync(Guid id)
    {
        return _context.Maintenances
            .Include(m => m.ChargingStation)
            .Include(m => m.ReportedByUser)
            .Include(m => m.AssignedToUser)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public Task<Maintenance?> GetByIdForCompanyAsync(Guid id, Guid companyId)
    {
        return _context.Maintenances
            .Include(m => m.ChargingStation)
            .Include(m => m.ReportedByUser)
            .Include(m => m.AssignedToUser)
            .FirstOrDefaultAsync(m => m.Id == id && m.ChargingStation != null && m.ChargingStation.CompanyId == companyId);
    }

    public Task<List<Maintenance>> GetByCompanyAsync(Guid companyId, bool includeResolved = true)
    {
        var query = _context.Maintenances
            .Include(m => m.ChargingStation)
            .Include(m => m.ReportedByUser)
            .Include(m => m.AssignedToUser)
            .Where(m => m.ChargingStation != null && m.ChargingStation.CompanyId == companyId);

        if (!includeResolved)
        {
            query = query.Where(m => m.Status != EMaintenanceStatus.Resolved);
        }

        return query
            .OrderByDescending(m => m.ReportedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Maintenance>> GetOpenIssuesByCompanyAsync(Guid companyId)
    {
        return _context.Maintenances
            .Include(m => m.ChargingStation)
            .Include(m => m.ReportedByUser)
            .Include(m => m.AssignedToUser)
            .Where(m => m.ChargingStation != null
                        && m.ChargingStation.CompanyId == companyId
                        && m.Status != EMaintenanceStatus.Resolved)
            .OrderByDescending(m => m.ReportedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Maintenance>> GetByStationIdAsync(Guid stationId)
    {
        return _context.Maintenances
            .Include(m => m.ChargingStation)
            .Include(m => m.ReportedByUser)
            .Include(m => m.AssignedToUser)
            .Where(m => m.ChargingStationId == stationId)
            .OrderByDescending(m => m.ReportedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<int> GetUnresolvedCountByStationAsync(Guid stationId)
    {
        return _context.Maintenances
            .Where(m => m.ChargingStationId == stationId && m.Status != EMaintenanceStatus.Resolved)
            .CountAsync();
    }

    public Task<bool> HasUnresolvedIssuesAsync(Guid stationId)
    {
        return _context.Maintenances
            .AnyAsync(m => m.ChargingStationId == stationId && m.Status != EMaintenanceStatus.Resolved);
    }

    public Task AddAsync(Maintenance issue)
    {
        return _context.Maintenances.AddAsync(issue).AsTask();
    }

    public void Update(Maintenance issue)
    {
        var trackedEntry = _context.ChangeTracker.Entries<Maintenance>()
            .FirstOrDefault(entry => entry.Entity.Id == issue.Id);

        if (trackedEntry != null)
        {
            trackedEntry.CurrentValues.SetValues(new
            {
                issue.ChargingStationId,
                issue.ReportedByUserId,
                issue.IssueDescription,
                issue.Status,
                issue.ReportedAt,
                issue.ResolvedAt,
                issue.AssignedToUserId,
                issue.Notes
            });
            return;
        }

        var update = new Maintenance
        {
            Id = issue.Id,
            ChargingStationId = issue.ChargingStationId,
            ReportedByUserId = issue.ReportedByUserId,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAt = issue.ReportedAt,
            ResolvedAt = issue.ResolvedAt,
            AssignedToUserId = issue.AssignedToUserId,
            Notes = issue.Notes
        };

        _context.Maintenances.Attach(update);
        _context.Entry(update).State = EntityState.Modified;
    }
}
