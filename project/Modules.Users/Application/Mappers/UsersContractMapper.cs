using Modules.Users.Application.DTO;
using Shared.Contracts.Users;

namespace Modules.Users.Application.Mappers;

internal static class UsersContractMapper
{
    public static CreateUserVehicleDto ToDto(CreateUserVehicleContract c) => new() { Make = c.Make, Model = c.Model, BatteryCapacity = c.BatteryCapacity, ConnectorIds = c.ConnectorIds };
    public static UpdateUserVehicleDto ToDto(UpdateUserVehicleContract c) => new() { Make = c.Make, Model = c.Model, BatteryCapacity = c.BatteryCapacity, ConnectorIds = c.ConnectorIds };
    public static UpdateUserProfileDto ToDto(UpdateUserProfileContract c) => new() { FirstName = c.FirstName, LastName = c.LastName, PhoneNumber = c.PhoneNumber };
    public static IssueRefreshTokenDto ToDto(IssueRefreshTokenContract c) => new() { UserId = c.UserId, ExpiresAtUtc = c.ExpiresAtUtc };
    public static RegisterCustomerDto ToDto(RegisterCustomerContract c) => new() { FirstName = c.FirstName, LastName = c.LastName, Email = c.Email, PhoneNumber = c.PhoneNumber, Password = c.Password };
    public static RegisterBasicUserDto ToDto(RegisterBasicUserContract c) => new() { Email = c.Email, Password = c.Password };

    public static UserVehicleContract ToContract(UserVehicleDto d) => new() { VehicleId = d.VehicleId, UserId = d.UserId, Make = d.Make, Model = d.Model, BatteryCapacity = d.BatteryCapacity, ConnectorIds = d.ConnectorIds };
    public static UserProfileContract ToContract(UserProfileDto d) => new() { UserId = d.UserId, Email = d.Email, FirstName = d.FirstName, LastName = d.LastName, PhoneNumber = d.PhoneNumber };
    public static ChangePasswordResultContract ToContract(ChangePasswordResultDto d) => new() { Success = d.Success, ErrorCode = d.ErrorCode, ErrorMessage = d.ErrorMessage };
    public static RenewRefreshTokenResultContract ToContract(RenewRefreshTokenResultDto d) => new() { Success = d.Success, ErrorCode = d.ErrorCode, ErrorMessage = d.ErrorMessage, RefreshToken = d.RefreshToken };
    public static RegisterCustomerResultContract ToContract(RegisterCustomerResultDto d) => new() { Success = d.Success, UserId = d.UserId, ErrorCode = d.ErrorCode, ErrorMessage = d.ErrorMessage };
    public static TwoFactorStatusContract ToContract(TwoFactorStatusDto d) => new() { UserExists = d.UserExists, IsTwoFactorEnabled = d.IsTwoFactorEnabled, RecoveryCodesLeft = d.RecoveryCodesLeft, HasAuthenticator = d.HasAuthenticator };
    public static TwoFactorSetupContract ToContract(TwoFactorSetupDto d) => new() { UserExists = d.UserExists, SharedKey = d.SharedKey, AuthenticatorUri = d.AuthenticatorUri, IsTwoFactorEnabled = d.IsTwoFactorEnabled, RecoveryCodesLeft = d.RecoveryCodesLeft };
    public static TwoFactorRecoveryCodesContract ToContract(TwoFactorRecoveryCodesDto d) => new() { UserExists = d.UserExists, Success = d.Success, ErrorMessage = d.ErrorMessage, RecoveryCodes = d.RecoveryCodes };
    public static DeleteAccountResultContract ToContract(DeleteAccountResultDto d) => new() { Success = d.Success, ErrorCode = d.ErrorCode, ErrorMessage = d.ErrorMessage };
    public static AuthenticateUserResultContract ToContract(AuthenticateUserResultDto d) => new() { Success = d.Success, UserId = d.UserId, ErrorCode = d.ErrorCode, ErrorMessage = d.ErrorMessage };
    public static RegisterBasicUserResultContract ToContract(RegisterBasicUserResultDto d) => new() { Success = d.Success, UserId = d.UserId, Errors = d.Errors };
    public static JwtClaimContract ToContract(JwtClaimDto d) => new() { Type = d.Type, Value = d.Value };
}
