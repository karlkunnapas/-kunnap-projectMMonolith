using System;
using System.Collections.Generic;
using App.Domain;

namespace App.BLL.DTOs;

public class HomePageFilterDto
{
    public string? Status { get; set; }
    public string? Connector { get; set; }
    public string? Location { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? UserId { get; set; }
}

public class HomePageDto
{
    public IReadOnlyList<HomeStationDto> Stations { get; set; } = new List<HomeStationDto>();
    public IReadOnlyList<string> ConnectorFilters { get; set; } = new List<string>();
}

public class HomeStationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> NameTranslations { get; set; } = new();
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<string> ConnectorNames { get; set; } = new();
    public bool? IsCompatibleWithSelectedVehicle { get; set; }
}
