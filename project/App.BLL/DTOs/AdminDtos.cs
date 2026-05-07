namespace App.BLL.DTOs;

public class AdminDashboardDto
{
    public int ReservationsInPeriod { get; set; }
    public int TotalCompanies { get; set; }
    public int TotalCompanyUsers { get; set; }
    public int TotalClientUsers { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public class AdminCompanyListItemDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveMemberCount { get; set; }
}

public class AdminCompanyListDto
{
    public string? Search { get; set; }
    public List<AdminCompanyListItemDto> Items { get; set; } = new();
}

public class AdminAuditLogFilterDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public Guid? EntityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AdminAuditLogListItemDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
    public string? ChangesJson { get; set; }
}

public class AdminAuditLogListDto
{
    public AdminAuditLogFilterDto Filter { get; set; } = new();
    public List<AdminAuditLogListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AdminStationListItemDto
{
    public Guid StationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
}

public class AdminStationListDto
{
    public string? Search { get; set; }
    public List<AdminStationListItemDto> Items { get; set; } = new();
}

public class AdminPromotionListItemDto
{
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public string CompanyName { get; set; } = "System Level";
    public bool IsSystemLevel { get; set; }
}

public class AdminPromotionListDto
{
    public List<AdminPromotionListItemDto> Items { get; set; } = new();
}

public class AdminPromotionFormDto
{
    public Guid? PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class AdminConnectorTypeListItemDto
{
    public Guid ConnectorTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class AdminConnectorTypeListDto
{
    public string? Search { get; set; }
    public List<AdminConnectorTypeListItemDto> Items { get; set; } = new();
}

public class AdminConnectorTypeFormDto
{
    public Guid? ConnectorTypeId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
