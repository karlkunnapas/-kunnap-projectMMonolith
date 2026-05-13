using Modules.Users.Application.Services;
using Shared.Contracts.Users;

namespace Modules.Users.Application;

internal sealed class UsersModuleApi : IUsersModuleApi
{
    private readonly IUsersApplicationService _service;

    public UsersModuleApi(IUsersApplicationService service)
    {
        _service = service;
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default) => _service.UserExistsAsync(userId, ct);
    public Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default) => _service.GetUserDisplayNameAsync(userId, ct);
    public Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => _service.GetVehicleConnectorIdsAsync(vehicleId, userId, ct);
    public Task<IReadOnlyCollection<UserVehicleContract>> GetUserVehiclesAsync(Guid userId, CancellationToken ct = default) => _service.GetUserVehiclesAsync(userId, ct);
    public Task<UserVehicleContract?> GetVehicleForUserAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => _service.GetVehicleForUserAsync(vehicleId, userId, ct);
    public Task<UserVehicleContract> CreateVehicleAsync(Guid userId, CreateUserVehicleContract request, CancellationToken ct = default) => _service.CreateVehicleAsync(userId, request, ct);
    public Task<UserVehicleContract?> UpdateVehicleAsync(Guid vehicleId, Guid userId, UpdateUserVehicleContract request, CancellationToken ct = default) => _service.UpdateVehicleAsync(vehicleId, userId, request, ct);
    public Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => _service.DeleteVehicleAsync(vehicleId, userId, ct);
    public Task<bool> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default) => _service.SetConnectorCompatibilityAsync(vehicleId, userId, connectorIds, ct);
    public Task<UserProfileContract?> GetUserProfileAsync(Guid userId, CancellationToken ct = default) => _service.GetUserProfileAsync(userId, ct);
    public Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileContract request, CancellationToken ct = default) => _service.UpdateUserProfileAsync(userId, request, ct);
    public Task<ChangePasswordResultContract> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default) => _service.ChangePasswordAsync(userId, currentPassword, newPassword, ct);
    public Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenContract request, CancellationToken ct = default) => _service.IssueRefreshTokenAsync(request, ct);
    public Task<RenewRefreshTokenResultContract> RenewRefreshTokenAsync(Guid userId, string refreshToken, DateTime newExpirationUtc, CancellationToken ct = default) => _service.RenewRefreshTokenAsync(userId, refreshToken, newExpirationUtc, ct);
    public Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default) => _service.RevokeRefreshTokenAsync(userId, refreshToken, ct);
    public Task<RegisterCustomerResultContract> RegisterCustomerAsync(RegisterCustomerContract request, CancellationToken ct = default) => _service.RegisterCustomerAsync(request, ct);
    public Task<TwoFactorStatusContract> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct = default) => _service.GetTwoFactorStatusAsync(userId, ct);
    public Task<TwoFactorSetupContract> StartTwoFactorSetupAsync(Guid userId, string appName, CancellationToken ct = default) => _service.StartTwoFactorSetupAsync(userId, appName, ct);
    public Task<TwoFactorRecoveryCodesContract> EnableTwoFactorAsync(Guid userId, string verificationCode, CancellationToken ct = default) => _service.EnableTwoFactorAsync(userId, verificationCode, ct);
    public Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default) => _service.DisableTwoFactorAsync(userId, ct);
    public Task<TwoFactorRecoveryCodesContract> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default) => _service.RegenerateRecoveryCodesAsync(userId, ct);
    public Task<DeleteAccountResultContract> DeleteAccountAsync(Guid userId, string? password, CancellationToken ct = default) => _service.DeleteAccountAsync(userId, password, ct);
    public Task<AuthenticateUserResultContract> AuthenticateByEmailAsync(string email, string password, CancellationToken ct = default) => _service.AuthenticateByEmailAsync(email, password, ct);
    public Task<RegisterBasicUserResultContract> RegisterBasicUserAsync(RegisterBasicUserContract request, CancellationToken ct = default) => _service.RegisterBasicUserAsync(request, ct);
    public Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default) => _service.GetUserIdByEmailAsync(email, ct);
    public Task<IReadOnlyCollection<JwtClaimContract>> GetJwtClaimsAsync(Guid userId, CancellationToken ct = default) => _service.GetJwtClaimsAsync(userId, ct);
}
