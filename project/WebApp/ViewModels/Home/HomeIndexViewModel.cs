using System;
using System.Collections.Generic;
using App.Domain;

namespace WebApp.ViewModels;

public class HomeIndexViewModel
{
    public bool ShowVehicleFilters { get; set; }
    public List<string> ConnectorFilters { get; set; } = new();
    public List<HomeStationViewModel> Stations { get; set; } = new();
}

public class HomeStationViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal MaxPower { get; set; }
    public List<string> ConnectorNames { get; set; } = new();
}
