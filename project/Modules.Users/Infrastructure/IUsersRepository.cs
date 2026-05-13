using Modules.Users.Application.DTO;

namespace Modules.Users.Infrastructure;

internal interface IUsersRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserVehicleDto>> GetUserVehiclesAsync(Guid userId, CancellationToken ct = default);
    Task<UserVehicleDto?> GetVehicleForUserAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<UserVehicleDto> CreateVehicleAsync(Guid userId, CreateUserVehicleDto request, CancellationToken ct = default);
    Task<UserVehicleDto?> UpdateVehicleAsync(Guid vehicleId, Guid userId, UpdateUserVehicleDto request, CancellationToken ct = default);
    Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<bool> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default);
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto request, CancellationToken ct = default);
    Task<ChangePasswordResultDto> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenDto request, CancellationToken ct = default);
    Task<RenewRefreshTokenResultDto> RenewRefreshTokenAsync(Guid userId, string refreshToken, DateTime newExpirationUtc, CancellationToken ct = default);
    Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default);
    Task<RegisterCustomerResultDto> RegisterCustomerAsync(RegisterCustomerDto request, CancellationToken ct = default);
    Task<TwoFactorStatusDto> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorSetupDto> StartTwoFactorSetupAsync(Guid userId, string appName, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesDto> EnableTwoFactorAsync(Guid userId, string verificationCode, CancellationToken ct = default);
    Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesDto> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default);
    Task<DeleteAccountResultDto> DeleteAccountAsync(Guid userId, string? password, CancellationToken ct = default);
    Task<AuthenticateUserResultDto> AuthenticateByEmailAsync(string email, string password, CancellationToken ct = default);
    Task<RegisterBasicUserResultDto> RegisterBasicUserAsync(RegisterBasicUserDto request, CancellationToken ct = default);
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyCollection<JwtClaimDto>> GetJwtClaimsAsync(Guid userId, CancellationToken ct = default);
}
