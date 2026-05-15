using Shared.Contracts;

namespace Modules.Charging.Domain;

internal sealed class Connector
{
    public Guid Id { get; set; }
    public LangStr Name { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public ICollection<ChargingStationConnector>? ChargingStationConnectors { get; set; }
}
