using App.Domain;

namespace App.BLL.DTOs;

public class ChargingSessionStartRequestDto
{
    public Guid StationId { get; set; }
    public Guid? ReservationId { get; set; }
}

public class ChargingSessionStopRequestDto
{
    public decimal EnergyConsumedKwh { get; set; }
    public int? DurationMinutes { get; set; }
}

public class ChargingSessionDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; }
}

public class ChargingSessionDetailsDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int DurationMinutes { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; }
}

public class ChargingSessionListDto
{
    public List<ChargingSessionDto> Sessions { get; set; } = new();
}

public class AuditEntryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime AtUtc { get; set; }
    public string? ChangesJson { get; set; }
}

public class AuditTrailDto
{
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public List<AuditEntryDto> Entries { get; set; } = new();
}
