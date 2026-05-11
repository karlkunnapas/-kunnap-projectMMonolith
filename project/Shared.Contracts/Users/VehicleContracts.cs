namespace Shared.Contracts.Users;

public sealed class CreateUserVehicleContract
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}

public sealed class UpdateUserVehicleContract
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}

public sealed class UserVehicleContract
{
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}
