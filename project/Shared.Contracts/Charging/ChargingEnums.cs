namespace Shared.Contracts.Charging;

public enum EStationStatus
{
    Available = 0,
    InUse = 2,
    Maintenance = 3
}

public enum EReservationStatus
{
    Active,
    Cancelled,
    Expired,
    Started
}

public enum EMaintenanceStatus
{
    Reported,
    InProgress,
    Resolved
}
