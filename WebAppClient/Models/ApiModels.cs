namespace WebAppClient.Models;

public class JwtResponseDto
{
    public string JWT { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    public string Jwt { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RegisterCustomerRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterCompanyRequestDto : RegisterCustomerRequestDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
}

public class UserCompaniesResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<UserCompanyItemDto> Companies { get; set; } = new();
}

public class UserCompanyItemDto
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserProfileResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UpdateUserProfileRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class TwoFactorStatusResponseDto
{
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool HasAuthenticator { get; set; }
}

public class TwoFactorSetupResponseDto
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

public class EnableTwoFactorRequestDto
{
    public string VerificationCode { get; set; } = string.Empty;
}

public class TwoFactorRecoveryCodesResponseDto
{
    public List<string> RecoveryCodes { get; set; } = new();
}

public class DeleteAccountRequestDto
{
    public string? Password { get; set; }
}

public class StationSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> NameTranslations { get; set; } = new();
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<string> ConnectorNames { get; set; } = new();
    public bool? IsCompatibleWithSelectedVehicle { get; set; }
}

public class StationDetailsDto
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<ConnectorDetailDto> Connectors { get; set; } = new();
    public List<StationReservationSlotDto> ExistingReservations { get; set; } = new();
    public List<AvailabilitySlotDto> AvailableSlots { get; set; } = new();
}

public class ConnectorDetailDto
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public List<TimeRangeDto> Reservations { get; set; } = new();
}

public class StationReservationSlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TimeRangeDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}

public class AvailabilitySlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAvailable { get; set; }
}

public class CostEstimateDto
{
    public decimal EstimatedCost { get; set; }
    public int DurationMinutes { get; set; }
}

public class ReportIssueRequestDto
{
    public string IssueDescription { get; set; } = string.Empty;
}

public class ReservationCreateRequestDto
{
    public Guid StationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public decimal? EstimatedEnergyKwh { get; set; }
    public string? PromotionCode { get; set; }
}

public class ReservationResponseDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
}

public class UserPromotionResponseDto
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsUsed { get; set; }
}

public class RedeemPromotionRequestDto
{
    public string Code { get; set; } = string.Empty;
}

public class SessionStartRequestDto
{
    public Guid StationId { get; set; }
    public Guid? ReservationId { get; set; }
}

public class SessionStopRequestDto
{
    public string? PromotionCode { get; set; }
}

public class SessionResponseDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
    public decimal Cost { get; set; }
    public decimal BaseCostBeforeDiscount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionCode { get; set; }
    public bool IsActive { get; set; }
}

public class SessionDetailResponseDto : SessionResponseDto
{
    public int DurationMinutes { get; set; }
}

public class VehicleCreateUpdateRequestDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public List<Guid> ConnectorIds { get; set; } = new();
}

public class VehicleResponseDto
{
    public Guid Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public List<VehicleConnectorResponseDto> CompatibleConnectors { get; set; } = new();
}

public class VehicleConnectorResponseDto
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DashboardResponseDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public DashboardKpisDto Kpis { get; set; } = new();
    public List<StationStatusCardDto> StationStatus { get; set; } = new();
    public List<MaintenanceIssueResponseDto> MaintenanceQueue { get; set; } = new();
    public List<ChartPointDto> UtilizationTrend { get; set; } = new();
    public List<ChartPointDto> RevenueTrend { get; set; } = new();
}

public class DashboardKpisDto
{
    public int TotalStations { get; set; }
    public int TotalReservations { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgUtilizationPercent { get; set; }
    public double AvgSessionDurationMinutes { get; set; }
    public string PeakHours { get; set; } = string.Empty;
}

public class StationStatusCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public decimal UtilizationPercent { get; set; }
    public int ActiveSessionsCount { get; set; }
    public int ReservationsToday { get; set; }
    public int PendingMaintenanceCount { get; set; }
    public decimal RevenueToday { get; set; }
}

public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class CompanyStationResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; }
    public List<string> Connectors { get; set; } = new();
    public int MaintenanceIssueCount { get; set; }
}

public class CompanyStationFormResponseDto
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<Guid> SelectedConnectorIds { get; set; } = new();
    public List<ConnectorAssignmentOptionDto> AvailableConnectors { get; set; } = new();
}

public class ConnectorAssignmentOptionDto
{
    public Guid ConnectorId { get; set; }
    public string ConnectorName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class CompanyStationUpsertRequestDto
{
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public int Status { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> SelectedConnectorIds { get; set; } = new();
}

public class StationStatusUpdateRequestDto
{
    public int Status { get; set; }
}

public class MaintenanceIssueResponseDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public string ReporterUserName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class MaintenanceStatusHistoryResponseDto
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}

public class MaintenanceStatusUpdateRequestDto
{
    public int Status { get; set; }
    public string? Notes { get; set; }
}

public class MaintenanceAssignmentRequestDto
{
    public Guid? AssignedToUserId { get; set; }
}

public class PromotionResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class PromotionUpsertRequestDto
{
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class CompanyUserResponseDto
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public class AddCompanyUserRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public int Role { get; set; }
}

public class AddCompanyUserResponseDto
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsExistingUser { get; set; }
    public string AccessStatus { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
}

public class UpdateCompanyUserRoleRequestDto
{
    public int Role { get; set; }
}
