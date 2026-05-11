namespace Shared.Contracts.Users;

public interface IUsersModuleApi
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserVehicleContract>> GetUserVehiclesAsync(Guid userId, CancellationToken ct = default);
    Task<UserVehicleContract?> GetVehicleForUserAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<UserVehicleContract> CreateVehicleAsync(Guid userId, CreateUserVehicleContract request, CancellationToken ct = default);
    Task<UserVehicleContract?> UpdateVehicleAsync(Guid vehicleId, Guid userId, UpdateUserVehicleContract request, CancellationToken ct = default);
    Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default);
    Task<bool> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default);
    Task<UserProfileContract?> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileContract request, CancellationToken ct = default);
    Task<ChangePasswordResultContract> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenContract request, CancellationToken ct = default);
    Task<RenewRefreshTokenResultContract> RenewRefreshTokenAsync(Guid userId, string refreshToken, DateTime newExpirationUtc, CancellationToken ct = default);
    Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default);
    Task<RegisterCustomerResultContract> RegisterCustomerAsync(RegisterCustomerContract request, CancellationToken ct = default);
    Task<TwoFactorStatusContract> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorSetupContract> StartTwoFactorSetupAsync(Guid userId, string appName, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesContract> EnableTwoFactorAsync(Guid userId, string verificationCode, CancellationToken ct = default);
    Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesContract> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default);
    Task<DeleteAccountResultContract> DeleteAccountAsync(Guid userId, string? password, CancellationToken ct = default);
    Task<AuthenticateUserResultContract> AuthenticateByEmailAsync(string email, string password, CancellationToken ct = default);
    Task<RegisterBasicUserResultContract> RegisterBasicUserAsync(RegisterBasicUserContract request, CancellationToken ct = default);
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyCollection<JwtClaimContract>> GetJwtClaimsAsync(Guid userId, CancellationToken ct = default);
}
