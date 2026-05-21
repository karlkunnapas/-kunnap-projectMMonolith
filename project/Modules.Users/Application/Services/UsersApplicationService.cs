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

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        return _usersRepository.UserExistsAsync(userId, ct);
    }

    public Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
    {
        return _usersRepository.GetUserDisplayNameAsync(userId, ct);
    }

    public Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(
        Guid vehicleId,
        Guid userId,
        CancellationToken ct = default)
    {
        return _usersRepository.GetVehicleConnectorIdsAsync(vehicleId, userId, ct);
    }

    public async Task<IReadOnlyCollection<UserVehicleContract>> GetUserVehiclesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var vehicles = await _usersRepository.GetUserVehiclesAsync(userId, ct);
        return vehicles
            .Select(UsersContractMapper.ToContract)
            .ToList();
    }

    public async Task<UserVehicleContract?> GetVehicleForUserAsync(
        Guid vehicleId,
        Guid userId,
        CancellationToken ct = default)
    {
        var dto = await _usersRepository.GetVehicleForUserAsync(vehicleId, userId, ct);
        return dto is { } ? UsersContractMapper.ToContract(dto) : null;
    }

    public async Task<UserVehicleContract> CreateVehicleAsync(
        Guid userId,
        CreateUserVehicleContract request,
        CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        var vehicle = await _usersRepository.CreateVehicleAsync(userId, dto, ct);
        return UsersContractMapper.ToContract(vehicle);
    }

    public async Task<UserVehicleContract?> UpdateVehicleAsync(
        Guid vehicleId,
        Guid userId,
        UpdateUserVehicleContract request,
        CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        var vehicle = await _usersRepository.UpdateVehicleAsync(vehicleId, userId, dto, ct);
        return vehicle is { } ? UsersContractMapper.ToContract(vehicle) : null;
    }

    public Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default)
    {
        return _usersRepository.DeleteVehicleAsync(vehicleId, userId, ct);
    }

    public Task<bool> SetConnectorCompatibilityAsync(
        Guid vehicleId,
        Guid userId,
        IReadOnlyCollection<Guid> connectorIds,
        CancellationToken ct = default)
    {
        return _usersRepository.SetConnectorCompatibilityAsync(vehicleId, userId, connectorIds, ct);
    }

    public async Task<UserProfileContract?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var dto = await _usersRepository.GetUserProfileAsync(userId, ct);
        return dto is { } ? UsersContractMapper.ToContract(dto) : null;
    }

    public Task<bool> UpdateUserProfileAsync(
        Guid userId,
        UpdateUserProfileContract request,
        CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        return _usersRepository.UpdateUserProfileAsync(userId, dto, ct);
    }

    public async Task<ChangePasswordResultContract> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var result = await _usersRepository.ChangePasswordAsync(userId, currentPassword, newPassword, ct);
        return UsersContractMapper.ToContract(result);
    }

    public Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenContract request, CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        return _usersRepository.IssueRefreshTokenAsync(dto, ct);
    }

    public async Task<RenewRefreshTokenResultContract> RenewRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        DateTime newExpirationUtc,
        CancellationToken ct = default)
    {
        var result = await _usersRepository.RenewRefreshTokenAsync(userId, refreshToken, newExpirationUtc, ct);
        return UsersContractMapper.ToContract(result);
    }

    public Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default)
    {
        return _usersRepository.RevokeRefreshTokenAsync(userId, refreshToken, ct);
    }

    public async Task<RegisterCustomerResultContract> RegisterCustomerAsync(
        RegisterCustomerContract request,
        CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        var result = await _usersRepository.RegisterCustomerAsync(dto, ct);
        return UsersContractMapper.ToContract(result);
    }

    public async Task<TwoFactorStatusContract> GetTwoFactorStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var status = await _usersRepository.GetTwoFactorStatusAsync(userId, ct);
        return UsersContractMapper.ToContract(status);
    }

    public async Task<TwoFactorSetupContract> StartTwoFactorSetupAsync(
        Guid userId,
        string appName,
        CancellationToken ct = default)
    {
        var setup = await _usersRepository.StartTwoFactorSetupAsync(userId, appName, ct);
        return UsersContractMapper.ToContract(setup);
    }

    public async Task<TwoFactorRecoveryCodesContract> EnableTwoFactorAsync(
        Guid userId,
        string verificationCode,
        CancellationToken ct = default)
    {
        var recoveryCodes = await _usersRepository.EnableTwoFactorAsync(userId, verificationCode, ct);
        return UsersContractMapper.ToContract(recoveryCodes);
    }

    public Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        return _usersRepository.DisableTwoFactorAsync(userId, ct);
    }

    public async Task<TwoFactorRecoveryCodesContract> RegenerateRecoveryCodesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var recoveryCodes = await _usersRepository.RegenerateRecoveryCodesAsync(userId, ct);
        return UsersContractMapper.ToContract(recoveryCodes);
    }

    public async Task<DeleteAccountResultContract> DeleteAccountAsync(
        Guid userId,
        string? password,
        CancellationToken ct = default)
    {
        var result = await _usersRepository.DeleteAccountAsync(userId, password, ct);
        return UsersContractMapper.ToContract(result);
    }

    public async Task<AuthenticateUserResultContract> AuthenticateByEmailAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var result = await _usersRepository.AuthenticateByEmailAsync(email, password, ct);
        return UsersContractMapper.ToContract(result);
    }

    public async Task<RegisterBasicUserResultContract> RegisterBasicUserAsync(
        RegisterBasicUserContract request,
        CancellationToken ct = default)
    {
        var dto = UsersContractMapper.ToDto(request);
        var result = await _usersRepository.RegisterBasicUserAsync(dto, ct);
        return UsersContractMapper.ToContract(result);
    }

    public Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        return _usersRepository.GetUserIdByEmailAsync(email, ct);
    }

    public async Task<IReadOnlyCollection<JwtClaimContract>> GetJwtClaimsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var claims = await _usersRepository.GetJwtClaimsAsync(userId, ct);
        return claims
            .Select(UsersContractMapper.ToContract)
            .ToList();
    }
}
