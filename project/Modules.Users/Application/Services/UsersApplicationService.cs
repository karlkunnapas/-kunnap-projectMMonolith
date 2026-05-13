using Modules.Users.Application.Mappers;
using Modules.Users.Infrastructure;
using Shared.Contracts.Users;

namespace Modules.Users.Application.Services;

internal sealed class UsersApplicationService : IUsersApplicationService
{
    private readonly IUsersRepository _usersRepository;

    public UsersApplicationService(IUsersRepository usersRepository)
    {
        _usersRepository = usersRepository;
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default) => _usersRepository.UserExistsAsync(userId, ct);
    public Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default) => _usersRepository.GetUserDisplayNameAsync(userId, ct);
    public Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => _usersRepository.GetVehicleConnectorIdsAsync(vehicleId, userId, ct);
    public async Task<IReadOnlyCollection<UserVehicleContract>> GetUserVehiclesAsync(Guid userId, CancellationToken ct = default) => (await _usersRepository.GetUserVehiclesAsync(userId, ct)).Select(UsersContractMapper.ToContract).ToList();
    public async Task<UserVehicleContract?> GetVehicleForUserAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => (await _usersRepository.GetVehicleForUserAsync(vehicleId, userId, ct)) is { } dto ? UsersContractMapper.ToContract(dto) : null;
    public async Task<UserVehicleContract> CreateVehicleAsync(Guid userId, CreateUserVehicleContract request, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.CreateVehicleAsync(userId, UsersContractMapper.ToDto(request), ct));
    public async Task<UserVehicleContract?> UpdateVehicleAsync(Guid vehicleId, Guid userId, UpdateUserVehicleContract request, CancellationToken ct = default) => (await _usersRepository.UpdateVehicleAsync(vehicleId, userId, UsersContractMapper.ToDto(request), ct)) is { } dto ? UsersContractMapper.ToContract(dto) : null;
    public Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default) => _usersRepository.DeleteVehicleAsync(vehicleId, userId, ct);
    public Task<bool> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default) => _usersRepository.SetConnectorCompatibilityAsync(vehicleId, userId, connectorIds, ct);
    public async Task<UserProfileContract?> GetUserProfileAsync(Guid userId, CancellationToken ct = default) => (await _usersRepository.GetUserProfileAsync(userId, ct)) is { } dto ? UsersContractMapper.ToContract(dto) : null;
    public Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileContract request, CancellationToken ct = default) => _usersRepository.UpdateUserProfileAsync(userId, UsersContractMapper.ToDto(request), ct);
    public async Task<ChangePasswordResultContract> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.ChangePasswordAsync(userId, currentPassword, newPassword, ct));
    public Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenContract request, CancellationToken ct = default) => _usersRepository.IssueRefreshTokenAsync(UsersContractMapper.ToDto(request), ct);
    public async Task<RenewRefreshTokenResultContract> RenewRefreshTokenAsync(Guid userId, string refreshToken, DateTime newExpirationUtc, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.RenewRefreshTokenAsync(userId, refreshToken, newExpirationUtc, ct));
    public Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default) => _usersRepository.RevokeRefreshTokenAsync(userId, refreshToken, ct);
    public async Task<RegisterCustomerResultContract> RegisterCustomerAsync(RegisterCustomerContract request, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.RegisterCustomerAsync(UsersContractMapper.ToDto(request), ct));
    public async Task<TwoFactorStatusContract> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.GetTwoFactorStatusAsync(userId, ct));
    public async Task<TwoFactorSetupContract> StartTwoFactorSetupAsync(Guid userId, string appName, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.StartTwoFactorSetupAsync(userId, appName, ct));
    public async Task<TwoFactorRecoveryCodesContract> EnableTwoFactorAsync(Guid userId, string verificationCode, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.EnableTwoFactorAsync(userId, verificationCode, ct));
    public Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default) => _usersRepository.DisableTwoFactorAsync(userId, ct);
    public async Task<TwoFactorRecoveryCodesContract> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.RegenerateRecoveryCodesAsync(userId, ct));
    public async Task<DeleteAccountResultContract> DeleteAccountAsync(Guid userId, string? password, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.DeleteAccountAsync(userId, password, ct));
    public async Task<AuthenticateUserResultContract> AuthenticateByEmailAsync(string email, string password, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.AuthenticateByEmailAsync(email, password, ct));
    public async Task<RegisterBasicUserResultContract> RegisterBasicUserAsync(RegisterBasicUserContract request, CancellationToken ct = default) => UsersContractMapper.ToContract(await _usersRepository.RegisterBasicUserAsync(UsersContractMapper.ToDto(request), ct));
    public Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default) => _usersRepository.GetUserIdByEmailAsync(email, ct);
    public async Task<IReadOnlyCollection<JwtClaimContract>> GetJwtClaimsAsync(Guid userId, CancellationToken ct = default) => (await _usersRepository.GetJwtClaimsAsync(userId, ct)).Select(UsersContractMapper.ToContract).ToList();
}
