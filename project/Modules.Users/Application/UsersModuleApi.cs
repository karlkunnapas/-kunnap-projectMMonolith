using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text;
using Modules.Users.Infrastructure;
using Shared.Contracts.Users;

namespace Modules.Users.Application;

internal sealed class UsersModuleApi : IUsersModuleApi
{
    private const string CustomerRole = "Customer";

    private readonly UsersDbContext _dbContext;
    private readonly IPasswordHasher<Domain.AppUser> _passwordHasher;
    private readonly UserManager<Domain.AppUser> _userManager;

    public UsersModuleApi(
        UsersDbContext dbContext,
        IPasswordHasher<Domain.AppUser> passwordHasher,
        UserManager<Domain.AppUser> userManager)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _userManager = userManager;
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Users.AnyAsync(u => u.Id == userId, ct);
    }

    public async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user?.Email ?? user?.UserName;
    }

    public async Task<IReadOnlyCollection<Guid>> GetVehicleConnectorIdsAsync(Guid vehicleId, Guid userId, CancellationToken ct = default)
    {
        var hasOwnership = await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.Id == vehicleId && v.UserId == userId, ct);

        if (!hasOwnership)
        {
            return Array.Empty<Guid>();
        }

        var connectorIds = await _dbContext.VehicleConnectors
            .AsNoTracking()
            .Where(vc => vc.VehicleId == vehicleId)
            .Select(vc => vc.ConnectorId)
            .Distinct()
            .ToListAsync(ct);

        return connectorIds;
    }

    public async Task<IReadOnlyCollection<UserVehicleContract>> GetUserVehiclesAsync(Guid userId, CancellationToken ct = default)
    {
        var vehicles = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderBy(v => v.Make)
            .ThenBy(v => v.Model)
            .Select(v => new UserVehicleContract
            {
                VehicleId = v.Id,
                UserId = v.UserId,
                Make = v.Make,
                Model = v.Model,
                BatteryCapacity = v.BatteryCapacity,
                ConnectorIds = Array.Empty<Guid>()
            })
            .ToListAsync(ct);

        if (vehicles.Count == 0)
        {
            return vehicles;
        }

        var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();
        var connectorLookup = await _dbContext.VehicleConnectors
            .AsNoTracking()
            .Where(vc => vehicleIds.Contains(vc.VehicleId))
            .GroupBy(vc => vc.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,
                ConnectorIds = g.Select(x => x.ConnectorId).Distinct().ToList()
            })
            .ToDictionaryAsync(x => x.VehicleId, x => (IReadOnlyCollection<Guid>)x.ConnectorIds, ct);

        foreach (var vehicle in vehicles)
        {
            vehicle.ConnectorIds = connectorLookup.TryGetValue(vehicle.VehicleId, out var ids)
                ? ids
                : Array.Empty<Guid>();
        }

        return vehicles;
    }

    public async Task<UserVehicleContract?> GetVehicleForUserAsync(Guid vehicleId, Guid userId, CancellationToken ct = default)
    {
        var vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId && v.UserId == userId)
            .Select(v => new UserVehicleContract
            {
                VehicleId = v.Id,
                UserId = v.UserId,
                Make = v.Make,
                Model = v.Model,
                BatteryCapacity = v.BatteryCapacity,
                ConnectorIds = Array.Empty<Guid>()
            })
            .FirstOrDefaultAsync(ct);

        if (vehicle == null)
        {
            return null;
        }

        vehicle.ConnectorIds = await _dbContext.VehicleConnectors
            .AsNoTracking()
            .Where(vc => vc.VehicleId == vehicleId)
            .Select(vc => vc.ConnectorId)
            .Distinct()
            .ToListAsync(ct);

        return vehicle;
    }

    public async Task<UserVehicleContract> CreateVehicleAsync(Guid userId, CreateUserVehicleContract request, CancellationToken ct = default)
    {
        var vehicle = new Domain.Vehicle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Make = request.Make.Trim(),
            Model = request.Model.Trim(),
            BatteryCapacity = request.BatteryCapacity
        };

        _dbContext.Vehicles.Add(vehicle);

        foreach (var connectorId in request.ConnectorIds.Distinct())
        {
            _dbContext.VehicleConnectors.Add(new Domain.VehicleConnector
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicle.Id,
                ConnectorId = connectorId
            });
        }

        await _dbContext.SaveChangesAsync(ct);

        return (await GetVehicleForUserAsync(vehicle.Id, userId, ct))!;
    }

    public async Task<UserVehicleContract?> UpdateVehicleAsync(Guid vehicleId, Guid userId, UpdateUserVehicleContract request, CancellationToken ct = default)
    {
        var vehicle = await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId, ct);

        if (vehicle == null) return null;

        vehicle.Make = request.Make.Trim();
        vehicle.Model = request.Model.Trim();
        vehicle.BatteryCapacity = request.BatteryCapacity;

        var existingLinks = await _dbContext.VehicleConnectors
            .Where(vc => vc.VehicleId == vehicleId)
            .ToListAsync(ct);

        _dbContext.VehicleConnectors.RemoveRange(existingLinks);
        foreach (var connectorId in request.ConnectorIds.Distinct())
        {
            _dbContext.VehicleConnectors.Add(new Domain.VehicleConnector
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                ConnectorId = connectorId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetVehicleForUserAsync(vehicleId, userId, ct);
    }

    public async Task<bool> DeleteVehicleAsync(Guid vehicleId, Guid userId, CancellationToken ct = default)
    {
        var vehicle = await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId, ct);

        if (vehicle == null) return false;

        var existingLinks = await _dbContext.VehicleConnectors
            .Where(vc => vc.VehicleId == vehicleId)
            .ToListAsync(ct);

        _dbContext.VehicleConnectors.RemoveRange(existingLinks);
        _dbContext.Vehicles.Remove(vehicle);

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default)
    {
        var vehicleExists = await _dbContext.Vehicles
            .AnyAsync(v => v.Id == vehicleId && v.UserId == userId, ct);

        if (!vehicleExists) return false;

        var existingLinks = await _dbContext.VehicleConnectors
            .Where(vc => vc.VehicleId == vehicleId)
            .ToListAsync(ct);

        _dbContext.VehicleConnectors.RemoveRange(existingLinks);
        foreach (var connectorId in connectorIds.Distinct())
        {
            _dbContext.VehicleConnectors.Add(new Domain.VehicleConnector
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                ConnectorId = connectorId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UserProfileContract?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return null;

        var claims = await _dbContext.UserClaims
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);

        var firstName = claims.FirstOrDefault(c => c.ClaimType == ClaimTypes.GivenName)?.ClaimValue ?? string.Empty;
        var lastName = claims.FirstOrDefault(c => c.ClaimType == ClaimTypes.Surname)?.ClaimValue ?? string.Empty;

        return new UserProfileContract
        {
            UserId = userId,
            Email = user.Email ?? string.Empty,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };
    }

    public async Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileContract request, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return false;

        user.PhoneNumber = request.PhoneNumber.Trim();

        var existingClaims = await _dbContext.UserClaims
            .Where(c => c.UserId == userId &&
                        (c.ClaimType == ClaimTypes.GivenName || c.ClaimType == ClaimTypes.Surname))
            .ToListAsync(ct);

        _dbContext.UserClaims.RemoveRange(existingClaims);
        _dbContext.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = userId,
            ClaimType = ClaimTypes.GivenName,
            ClaimValue = request.FirstName.Trim()
        });
        _dbContext.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = userId,
            ClaimType = ClaimTypes.Surname,
            ClaimValue = request.LastName.Trim()
        });

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ChangePasswordResultContract> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return ChangePasswordResultContract.Fail("NOT_FOUND", "User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return ChangePasswordResultContract.Fail("INVALID_PASSWORD", "Current password is invalid.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            return ChangePasswordResultContract.Fail("INVALID_PASSWORD", "Current password is invalid.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        await _dbContext.SaveChangesAsync(ct);
        return ChangePasswordResultContract.Ok();
    }

    public async Task<string?> IssueRefreshTokenAsync(IssueRefreshTokenContract request, CancellationToken ct = default)
    {
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UserId, ct);

        if (!userExists)
        {
            return null;
        }

        if (!_dbContext.Database.ProviderName!.Contains("InMemory"))
        {
            await _dbContext
                .RefreshTokens
                .Where(t => t.UserId == request.UserId && t.Expiration < DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
        }

        var refreshToken = new Domain.AppRefreshToken
        {
            UserId = request.UserId,
            Expiration = request.ExpiresAtUtc
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(ct);

        return refreshToken.RefreshToken;
    }

    public async Task<RenewRefreshTokenResultContract> RenewRefreshTokenAsync(Guid userId, string refreshToken, DateTime newExpirationUtc, CancellationToken ct = default)
    {
        var normalizedRefreshToken = refreshToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedRefreshToken))
        {
            return RenewRefreshTokenResultContract.Fail("INVALID", "Refresh token is invalid.");
        }

        var matches = await _dbContext.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                (
                    (x.RefreshToken == normalizedRefreshToken && x.Expiration > DateTime.UtcNow) ||
                    (x.PreviousRefreshToken == normalizedRefreshToken && x.PreviousExpiration > DateTime.UtcNow)
                ))
            .ToListAsync(ct);

        if (matches.Count == 0)
        {
            return RenewRefreshTokenResultContract.Fail("INVALID_OR_EXPIRED", "Refresh token is invalid or expired.");
        }

        if (matches.Count != 1)
        {
            return RenewRefreshTokenResultContract.Fail("MULTIPLE_VALID", "More than one valid refresh token found.");
        }

        var match = matches[0];
        if (match.RefreshToken == normalizedRefreshToken)
        {
            match.PreviousRefreshToken = match.RefreshToken;
            match.PreviousExpiration = DateTime.UtcNow.AddMinutes(1);
            match.RefreshToken = Guid.NewGuid().ToString();
            match.Expiration = newExpirationUtc;
            await _dbContext.SaveChangesAsync(ct);
        }

        return RenewRefreshTokenResultContract.Ok(match.RefreshToken);
    }

    public async Task<int> RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var normalizedRefreshToken = refreshToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedRefreshToken))
        {
            return 0;
        }

        if (!_dbContext.Database.ProviderName!.Contains("InMemory"))
        {
            return await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId &&
                            (x.RefreshToken == normalizedRefreshToken || x.PreviousRefreshToken == normalizedRefreshToken))
                .ExecuteDeleteAsync(ct);
        }

        var matches = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId &&
                        (x.RefreshToken == normalizedRefreshToken || x.PreviousRefreshToken == normalizedRefreshToken))
            .ToListAsync(ct);

        _dbContext.RefreshTokens.RemoveRange(matches);
        await _dbContext.SaveChangesAsync(ct);
        return matches.Count;
    }

    public async Task<RegisterCustomerResultContract> RegisterCustomerAsync(RegisterCustomerContract request, CancellationToken ct = default)
    {
        var existing = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == request.Email.Trim().ToUpperInvariant(), ct);
        if (existing)
        {
            return RegisterCustomerResultContract.Fail("DUPLICATE_EMAIL", "A user with this email already exists.");
        }

        var user = new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            NormalizedUserName = request.Email.Trim().ToUpperInvariant(),
            Email = request.Email.Trim(),
            NormalizedEmail = request.Email.Trim().ToUpperInvariant(),
            PhoneNumber = request.PhoneNumber.Trim(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return RegisterCustomerResultContract.Fail("USER_CREATION_FAILED", message);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded)
        {
            var message = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            return RegisterCustomerResultContract.Fail("ROLE_ASSIGNMENT_FAILED", message);
        }

        return RegisterCustomerResultContract.Ok(user.Id);
    }

    public async Task<TwoFactorStatusContract> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new TwoFactorStatusContract { UserExists = false };
        }

        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
        var hasAuthenticator = !string.IsNullOrWhiteSpace(await _userManager.GetAuthenticatorKeyAsync(user));

        return new TwoFactorStatusContract
        {
            UserExists = true,
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            RecoveryCodesLeft = recoveryCodesLeft,
            HasAuthenticator = hasAuthenticator
        };
    }

    public async Task<TwoFactorSetupContract> StartTwoFactorSetupAsync(Guid userId, string appName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new TwoFactorSetupContract { UserExists = false };
        }

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        key ??= string.Empty;
        var email = await _userManager.GetEmailAsync(user) ?? user.UserName ?? "user";
        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

        return new TwoFactorSetupContract
        {
            UserExists = true,
            SharedKey = FormatKey(key),
            AuthenticatorUri = GenerateQrCodeUri(appName, email, key),
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            RecoveryCodesLeft = recoveryCodesLeft
        };
    }

    public async Task<TwoFactorRecoveryCodesContract> EnableTwoFactorAsync(Guid userId, string verificationCode, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new TwoFactorRecoveryCodesContract { UserExists = false };
        }

        var normalizedCode = verificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            normalizedCode);

        if (!isValid)
        {
            return new TwoFactorRecoveryCodesContract
            {
                UserExists = true,
                Success = false,
                ErrorMessage = "Verification code is invalid."
            };
        }

        var setResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!setResult.Succeeded)
        {
            return new TwoFactorRecoveryCodesContract
            {
                UserExists = true,
                Success = false,
                ErrorMessage = string.Join(", ", setResult.Errors.Select(e => e.Description))
            };
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesContract
        {
            UserExists = true,
            Success = true,
            RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).ToList()
        };
    }

    public async Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        return true;
    }

    public async Task<TwoFactorRecoveryCodesContract> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new TwoFactorRecoveryCodesContract { UserExists = false };
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesContract
        {
            UserExists = true,
            Success = true,
            RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).ToList()
        };
    }

    public async Task<DeleteAccountResultContract> DeleteAccountAsync(Guid userId, string? password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return DeleteAccountResultContract.Fail("NOT_FOUND", "User not found.");
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (hasPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return DeleteAccountResultContract.Fail("PASSWORD_REQUIRED", "Password is required.");
            }

            var validPassword = await _userManager.CheckPasswordAsync(user, password);
            if (!validPassword)
            {
                return DeleteAccountResultContract.Fail("INVALID_PASSWORD", "Incorrect password.");
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return DeleteAccountResultContract.Fail(
                "DELETE_FAILED",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return DeleteAccountResultContract.Ok();
    }

    public async Task<AuthenticateUserResultContract> AuthenticateByEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return AuthenticateUserResultContract.Fail("USER_NOT_FOUND", "User not found.");
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!validPassword)
        {
            return AuthenticateUserResultContract.Fail("INVALID_PASSWORD", "Invalid password.");
        }

        return AuthenticateUserResultContract.Ok(user.Id);
    }

    public async Task<RegisterBasicUserResultContract> RegisterBasicUserAsync(RegisterBasicUserContract request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existing != null)
        {
            return new RegisterBasicUserResultContract
            {
                Success = false,
                Errors = new[] { "User already registered" }
            };
        }

        var user = new Domain.AppUser
        {
            Email = request.Email.Trim(),
            UserName = request.Email.Trim()
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new RegisterBasicUserResultContract
            {
                Success = false,
                Errors = createResult.Errors.Select(e => e.Description).ToList()
            };
        }

        return new RegisterBasicUserResultContract
        {
            Success = true,
            UserId = user.Id
        };
    }

    public async Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<JwtClaimContract>> GetJwtClaimsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Array.Empty<JwtClaimContract>();
        }

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim("email", user.Email));
            claims.Add(new Claim(ClaimTypes.Name, user.Email));
        }

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var userClaims = await _userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);

        return claims
            .GroupBy(c => new { c.Type, c.Value })
            .Select(g => new JwtClaimContract
            {
                Type = g.Key.Type,
                Value = g.Key.Value
            })
            .ToList();
    }

    private static string GenerateQrCodeUri(string appName, string email, string unformattedKey)
    {
        return string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            Uri.EscapeDataString(appName),
            Uri.EscapeDataString(email),
            unformattedKey);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }
}
