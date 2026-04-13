using WebAppClient.Enums;

namespace WebAppClient.Helpers;

public static class EnumParser
{
    public static EStationStatus ParseStation(string value)
    {
        return Enum.TryParse<EStationStatus>(value, true, out var parsed)
            ? parsed
            : EStationStatus.Available;
    }

    public static EReservationStatus ParseReservation(string value)
    {
        return Enum.TryParse<EReservationStatus>(value, true, out var parsed)
            ? parsed
            : EReservationStatus.Active;
    }

    public static EMaintenanceStatus ParseMaintenance(string value)
    {
        return Enum.TryParse<EMaintenanceStatus>(value, true, out var parsed)
            ? parsed
            : EMaintenanceStatus.Reported;
    }

    public static ECompanyRole ParseCompanyRole(string value)
    {
        return Enum.TryParse<ECompanyRole>(value, true, out var parsed)
            ? parsed
            : ECompanyRole.Employee;
    }
}
