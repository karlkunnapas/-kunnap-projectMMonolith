using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Company;

public class CompanyStationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; }
    public List<string> Connectors { get; set; } = new();
    public int MaintenanceIssueCount { get; set; }
}

public class CompanyStationFormResponse
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public string NameEn { get; set; } = default!;
    public string NameEt { get; set; } = default!;
    public string Location { get; set; } = default!;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public string Status { get; set; } = default!;
    public bool IsActive { get; set; }
    public List<Guid> SelectedConnectorIds { get; set; } = new();
    public List<ConnectorAssignmentOption> AvailableConnectors { get; set; } = new();
}

public class ConnectorAssignmentOption
{
    public Guid ConnectorId { get; set; }
    public string ConnectorName { get; set; } = default!;
    public bool IsAssigned { get; set; }
}

public class CompanyStationUpsert
{
    [Required]
    [MaxLength(200)]
    public string NameEn { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string NameEt { get; set; } = default!;

    [Required]
    [MaxLength(300)]
    public string Location { get; set; } = default!;

    [Range(0, 100)]
    public decimal PricePerKwh { get; set; }

    [Range(1, 500)]
    public decimal MaxPower { get; set; }

    public int Status { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> SelectedConnectorIds { get; set; } = new();
}

public class StationStatusUpdate
{
    [Required]
    public int Status { get; set; }
}

public class StationActivationUpdate
{
    [Required]
    public bool IsActive { get; set; }
}
