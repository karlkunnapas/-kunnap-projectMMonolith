using System;
using System.Collections.Generic;
using WebAppClient.Enums;

namespace WebApp.ViewModels;

public class HomeIndexViewModel
{
    public bool ShowVehicleFilters { get; set; }
    public List<string> ConnectorFilters { get; set; } = new();
    public List<HomeStationViewModel> Stations { get; set; } = new();
    public List<HomeVehicleOptionViewModel> VehicleOptions { get; set; } = new();

    public string? SelectedStatus { get; set; }
    public string? SelectedConnector { get; set; }
    public string? LocationQuery { get; set; }
    public Guid? SelectedVehicleId { get; set; }
}

public class HomeStationViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<string> ConnectorNames { get; set; } = new();
    public bool? IsCompatibleWithSelectedVehicle { get; set; }
}

public class HomeVehicleOptionViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
