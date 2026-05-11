using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using System.Security.Claims;

namespace App.BLL.Services;

public class IdentityService : IIdentityService
{
    private const string CustomerRole = "Customer";
    private const string CompanyOwnerRole = "CompanyOwner";

    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IAuditService _auditService;

    public IdentityService(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IUsersModuleApi usersModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _usersModuleApi = usersModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _auditService = auditService;
    }

    public async Task<SignInResult> LoginAsync(string email, string password, bool rememberMe)
    {
        return await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<ServiceResult<UserCompanyListResultDto>> GetUserCompaniesAsync(Guid userId)
    {
        var userExists = await _usersModuleApi.UserExistsAsync(userId);
        if (!userExists)
        {
            return ServiceResult<UserCompanyListResultDto>.Fail("USER_NOT_FOUND", "User not found.");
        }

        var userCompanies = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        var companyDtos = userCompanies
            .Select(uc => new CompanySelectionItemDto
            {
                MembershipId = uc.MembershipId,
                CompanyId = uc.CompanyId,
                CompanyName = uc.CompanyName,
                CompanySlug = uc.Slug,
                Role = uc.Role
            })
            .ToList();
        var userDisplay = await _usersModuleApi.GetUserDisplayNameAsync(userId) ?? string.Empty;
        var result = BllDtoFactory.CreateUserCompanyListResultDto(userId, userDisplay, companyDtos);

        return ServiceResult<UserCompanyListResultDto>.Ok(result);
    }

    public async Task<ServiceResult<List<CompanyUserMembershipDto>>> GetCompanyUsersAsync(Guid companyId, Guid ownerUserId)
    {
        if (companyId == Guid.Empty || ownerUserId == Guid.Empty)
        {
            return ServiceResult<List<CompanyUserMembershipDto>>.Fail("VALIDATION", "Company and owner identifiers are required.");
        }

        var ownerValidation = await EnsureOwnerAsync(companyId, ownerUserId);
        if (!ownerValidation.Success)
        {
            return ServiceResult<List<CompanyUserMembershipDto>>.Fail(ownerValidation.Errors);
        }

        var memberships = await _companiesModuleApi.GetCompanyMembershipsAsync(companyId);
        var result = new List<CompanyUserMembershipDto>(memberships.Count);
        foreach (var membership in memberships)
        {
            var display = await _usersModuleApi.GetUserDisplayNameAsync(membership.UserId) ?? string.Empty;
            result.Add(new CompanyUserMembershipDto
            {
                MembershipId = membership.MembershipId,
                UserId = membership.UserId,
                Email = display,
                Role = ParseCompanyRole(membership.Role),
                IsActive = membership.IsActive,
                JoinedAtUtc = membership.JoinedAtUtc
            });
        }

        return ServiceResult<List<CompanyUserMembershipDto>>.Ok(result);
    }

    public async Task<ServiceResult<CompanyUserMembershipDto>> GetCompanyUserMembershipAsync(Guid companyId, Guid ownerUserId, Guid membershipId)
    {
        if (companyId == Guid.Empty || ownerUserId == Guid.Empty || membershipId == Guid.Empty)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("VALIDATION", "Company, owner, and membership identifiers are required.");
        }

        var ownerValidation = await EnsureOwnerAsync(companyId, ownerUserId);
        if (!ownerValidation.Success)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail(ownerValidation.Errors);
        }

        var membership = await _companiesModuleApi.GetCompanyMembershipAsync(companyId, membershipId);

        if (membership == null)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("NOT_FOUND", "Membership not found.");
        }

        var display = await _usersModuleApi.GetUserDisplayNameAsync(membership.UserId) ?? string.Empty;
        return ServiceResult<CompanyUserMembershipDto>.Ok(new CompanyUserMembershipDto
        {
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            Email = display,
            Role = ParseCompanyRole(membership.Role),
            IsActive = membership.IsActive,
            JoinedAtUtc = membership.JoinedAtUtc
        });
    }

    public async Task<ServiceResult<AddCompanyUserResultDto>> AddUserToCompanyAsync(
        Guid companyId,
        Guid ownerUserId,
        string ownerUserName,
        AddCompanyUserRequestDto dto)
    {
        if (companyId == Guid.Empty || ownerUserId == Guid.Empty)
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail("VALIDATION", "Company and owner identifiers are required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail("VALIDATION", "Email is required.");
        }

        if (!Enum.IsDefined(dto.Role))
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail("INVALID_ROLE", "Invalid company role.");
        }

        var ownerValidation = await EnsureOwnerAsync(companyId, ownerUserId);
        if (!ownerValidation.Success)
        {
            await _auditService.LogMutationAsync(
                companyId,
                ownerUserName,
                nameof(AppUserCompany),
                ownerUserId,
                "UnauthorizedAddAttempt",
                $"{{\"email\":\"{dto.Email.Trim()}\",\"role\":\"{dto.Role}\"}}");

            return ServiceResult<AddCompanyUserResultDto>.Fail(ownerValidation.Errors);
        }

        var normalizedEmail = _userManager.NormalizeEmail(dto.Email.Trim());
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail("VALIDATION", "Email is invalid.");
        }

        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (existingUser != null)
        {
            return await LinkExistingUserMembershipAsync(
                companyId,
                ownerUserName,
                existingUser,
                dto.Role);
        }

        return await CreateNewUserAndMembershipAsync(
            companyId,
            ownerUserName,
            dto);
    }

    public async Task<ServiceResult<CompanyUserMembershipDto>> UpdateCompanyUserRoleAsync(
        Guid companyId,
        Guid ownerUserId,
        string ownerUserName,
        Guid membershipId,
        UpdateCompanyUserRoleRequestDto dto)
    {
        if (companyId == Guid.Empty || ownerUserId == Guid.Empty || membershipId == Guid.Empty)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("VALIDATION", "Company, owner, and membership identifiers are required.");
        }

        if (dto == null || !Enum.IsDefined(dto.Role))
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("INVALID_ROLE", "Invalid company role.");
        }

        var ownerValidation = await EnsureOwnerAsync(companyId, ownerUserId);
        if (!ownerValidation.Success)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail(ownerValidation.Errors);
        }

        var membership = await _companiesModuleApi.GetCompanyMembershipAsync(companyId, membershipId);

        if (membership == null)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("NOT_FOUND", "Membership not found.");
        }

        if (!membership.IsActive)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("MEMBERSHIP_INACTIVE", "Cannot edit inactive membership.");
        }

        if (ParseCompanyRole(membership.Role) == ECompanyRole.Owner && dto.Role != ECompanyRole.Owner)
        {
            var lastOwnerCheck = await PreventLastOwnerChangeAsync(companyId);
            if (!lastOwnerCheck.Success)
            {
                return ServiceResult<CompanyUserMembershipDto>.Fail(lastOwnerCheck.Errors);
            }
        }

        var updatedMembership = await _companiesModuleApi.UpdateCompanyMembershipRoleAsync(companyId, membershipId, dto.Role.ToString());
        if (updatedMembership == null)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("NOT_FOUND", "Membership not found.");
        }

        var targetUser = await _userManager.FindByIdAsync(updatedMembership.UserId.ToString());
        if (targetUser != null)
        {
            if (dto.Role == ECompanyRole.Owner)
            {
                var ensureRoleResult = await EnsureOwnerRoleClaimAsync(targetUser);
                if (!ensureRoleResult.Success)
                {
                    return ServiceResult<CompanyUserMembershipDto>.Fail("ROLE_ASSIGNMENT_FAILED", ensureRoleResult.ErrorMessage ?? "Failed to assign owner role.");
                }
            }
            else
            {
                var cleanupResult = await RemoveOwnerRoleIfNoMembershipsAsync(targetUser);
                if (!cleanupResult.Success)
                {
                    return ServiceResult<CompanyUserMembershipDto>.Fail("ROLE_ASSIGNMENT_FAILED", cleanupResult.ErrorMessage ?? "Failed to update owner role.");
                }
            }
        }

        await _auditService.LogMutationAsync(
            companyId,
            ownerUserName,
            nameof(AppUserCompany),
            updatedMembership.MembershipId,
            "MembershipRoleUpdated",
            $"{{\"targetUserId\":\"{updatedMembership.UserId}\",\"role\":\"{updatedMembership.Role}\"}}");

        return ServiceResult<CompanyUserMembershipDto>.Ok(new CompanyUserMembershipDto
        {
            MembershipId = updatedMembership.MembershipId,
            UserId = updatedMembership.UserId,
            Email = await _usersModuleApi.GetUserDisplayNameAsync(updatedMembership.UserId) ?? string.Empty,
            Role = ParseCompanyRole(updatedMembership.Role),
            IsActive = updatedMembership.IsActive,
            JoinedAtUtc = updatedMembership.JoinedAtUtc
        });
    }

    public async Task<ServiceResult> RemoveCompanyUserAsync(Guid companyId, Guid ownerUserId, string ownerUserName, Guid membershipId)
    {
        if (companyId == Guid.Empty || ownerUserId == Guid.Empty || membershipId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Company, owner, and membership identifiers are required.");
        }

        var ownerValidation = await EnsureOwnerAsync(companyId, ownerUserId);
        if (!ownerValidation.Success)
        {
            return ServiceResult.Fail(ownerValidation.Errors);
        }

        var membership = await _companiesModuleApi.GetCompanyMembershipAsync(companyId, membershipId);

        if (membership == null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Membership not found.");
        }

        if (!membership.IsActive)
        {
            return ServiceResult.Fail("MEMBERSHIP_INACTIVE", "Membership is already inactive.");
        }

        if (ParseCompanyRole(membership.Role) == ECompanyRole.Owner)
        {
            var lastOwnerCheck = await PreventLastOwnerChangeAsync(companyId);
            if (!lastOwnerCheck.Success)
            {
                return lastOwnerCheck;
            }
        }

        var removedMembership = await _companiesModuleApi.DeactivateCompanyMembershipAsync(companyId, membershipId);
        if (removedMembership == null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Membership not found.");
        }

        var membershipUser = await _userManager.FindByIdAsync(removedMembership.UserId.ToString());
        if (membershipUser != null)
        {
            var cleanupResult = await RemoveOwnerRoleIfNoMembershipsAsync(membershipUser);
            if (!cleanupResult.Success)
            {
                return ServiceResult.Fail("ROLE_ASSIGNMENT_FAILED", cleanupResult.ErrorMessage ?? "Failed to update owner role.");
            }
        }

        await _auditService.LogMutationAsync(
            companyId,
            ownerUserName,
            nameof(AppUserCompany),
            removedMembership.MembershipId,
            "MembershipRemoved",
            $"{{\"targetUserId\":\"{removedMembership.UserId}\",\"role\":\"{removedMembership.Role}\"}}");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CompanySelectionItemDto>> SetActiveCompanyAsync(Guid userId, Guid companyId, string actorUserName)
    {
        if (userId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanySelectionItemDto>.Fail("VALIDATION", "User and company identifiers are required.");
        }

        var membership = await _companiesModuleApi.GetActiveCompanySelectionAsync(userId, companyId);
        if (membership == null)
        {
            return ServiceResult<CompanySelectionItemDto>.Fail("FORBIDDEN", "Company selection is not allowed.");
        }

        await _auditService.LogMutationAsync(
            companyId,
            actorUserName,
            nameof(AppUserCompany),
            membership.MembershipId,
            "CompanySwitched",
            $"{{\"userId\":\"{userId}\",\"role\":\"{membership.Role}\"}}");

        return ServiceResult<CompanySelectionItemDto>.Ok(new CompanySelectionItemDto
        {
            MembershipId = membership.MembershipId,
            CompanyId = membership.CompanyId,
            CompanyName = membership.CompanyName,
            CompanySlug = membership.Slug,
            Role = membership.Role
        });
    }

    public async Task<ServiceResult<Guid>> RegisterCompanyOwnerAsync(RegisterCompanyOwnerDto dto)
    {
        // Check for duplicate email
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return ServiceResult<Guid>.Fail("DUPLICATE_EMAIL", "A user with this email already exists.");
        }

        // Create the user via UserManager
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            EmailConfirmed = true, // Auto-confirm since no email service
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult<Guid>.Fail("USER_CREATION_FAILED", 
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var ownerRoleResult = await _userManager.AddToRoleAsync(user, CompanyOwnerRole);
        if (!ownerRoleResult.Succeeded)
        {
            return ServiceResult<Guid>.Fail(
                "ROLE_ASSIGNMENT_FAILED",
                string.Join(", ", ownerRoleResult.Errors.Select(e => e.Description)));
        }

        var companyResult = await _companiesModuleApi.CreateCompanyWithOwnerMembershipAsync(new CreateCompanyWithOwnerMembershipContract
        {
            OwnerUserId = user.Id,
            ContactEmail = dto.Email,
            ContactPhone = dto.PhoneNumber,
            CompanyName = dto.CompanyName,
            CompanySlug = dto.CompanySlug
        });
        if (!companyResult.Success)
        {
            return ServiceResult<Guid>.Fail(
                companyResult.ErrorCode ?? "COMPANY_CREATION_FAILED",
                companyResult.ErrorMessage ?? "Company creation failed.");
        }

        return ServiceResult<Guid>.Ok(companyResult.CompanyId);
    }

    public async Task<ServiceResult<Guid>> RegisterCustomerAsync(RegisterCustomerDto dto)
    {
        var result = await _usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Password = dto.Password
        });

        if (!result.Success)
        {
            return ServiceResult<Guid>.Fail(
                result.ErrorCode ?? "USER_CREATION_FAILED",
                result.ErrorMessage ?? "Customer registration failed.");
        }

        return ServiceResult<Guid>.Ok(result.UserId);
    }

    private async Task<ServiceResult> EnsureOwnerAsync(Guid companyId, Guid ownerUserId)
    {
        var hasOwner = await _companiesModuleApi.HasActiveOwnerMembershipAsync(companyId, ownerUserId);
        return !hasOwner
            ? ServiceResult.Fail("NOT_OWNER", "Only company owners can manage users.")
            : ServiceResult.Ok();
    }

    private static ECompanyRole ParseCompanyRole(string role)
    {
        return Enum.TryParse<ECompanyRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : ECompanyRole.Employee;
    }

    private async Task<ServiceResult<AddCompanyUserResultDto>> LinkExistingUserMembershipAsync(
        Guid companyId,
        string ownerUserName,
        AppUser user,
        ECompanyRole role)
    {
        var existingMembership = await _companiesModuleApi.GetCompanyMembershipByUserAsync(companyId, user.Id);
        if (existingMembership != null)
        {
            if (existingMembership.IsActive)
            {
                return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
                    companyId,
                    existingMembership.MembershipId,
                    user.Id,
                    user.Email ?? string.Empty,
                    role,
                    isExistingUser: true,
                    accessStatus: "Already active",
                    nextAction: "User already has active company access.",
                    membershipAlreadyActive: true));
            }

            var reactivatedMembership = await _companiesModuleApi.UpsertCompanyMembershipAsync(new UpsertCompanyMembershipContract
            {
                CompanyId = companyId,
                UserId = user.Id,
                Role = role.ToString()
            });

            if (role == ECompanyRole.Owner)
            {
                var ensureRoleResult = await EnsureOwnerRoleClaimAsync(user);
                if (!ensureRoleResult.Success)
                {
                    return ServiceResult<AddCompanyUserResultDto>.Fail("ROLE_ASSIGNMENT_FAILED", ensureRoleResult.ErrorMessage ?? "Failed to assign owner role.");
                }
            }

            await _auditService.LogMutationAsync(
                companyId,
                ownerUserName,
                nameof(AppUserCompany),
                reactivatedMembership.Membership.MembershipId,
                "MembershipReactivated",
                $"{{\"targetUserId\":\"{user.Id}\",\"email\":\"{user.Email}\",\"role\":\"{role}\"}}");

            return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
                companyId,
                reactivatedMembership.Membership.MembershipId,
                user.Id,
                user.Email ?? string.Empty,
                role,
                isExistingUser: true,
                accessStatus: "Reactivated",
                nextAction: "Membership was reactivated and access is immediate.",
                membershipReactivated: true));
        }

        var createdMembership = await _companiesModuleApi.UpsertCompanyMembershipAsync(new UpsertCompanyMembershipContract
        {
            CompanyId = companyId,
            UserId = user.Id,
            Role = role.ToString()
        });

        if (role == ECompanyRole.Owner)
        {
            var ensureRoleResult = await EnsureOwnerRoleClaimAsync(user);
            if (!ensureRoleResult.Success)
            {
                return ServiceResult<AddCompanyUserResultDto>.Fail("ROLE_ASSIGNMENT_FAILED", ensureRoleResult.ErrorMessage ?? "Failed to assign owner role.");
            }
        }

        await _auditService.LogMutationAsync(
            companyId,
            ownerUserName,
            nameof(AppUserCompany),
            createdMembership.Membership.MembershipId,
            "ExistingUserLinked",
            $"{{\"targetUserId\":\"{user.Id}\",\"email\":\"{user.Email}\",\"role\":\"{role}\"}}");

        return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
            companyId,
            createdMembership.Membership.MembershipId,
            user.Id,
            user.Email ?? string.Empty,
            role,
            isExistingUser: true,
            accessStatus: "Linked",
            nextAction: "User can access the company immediately."));
    }

    private async Task<ServiceResult<AddCompanyUserResultDto>> CreateNewUserAndMembershipAsync(
        Guid companyId,
        string ownerUserName,
        AddCompanyUserRequestDto dto)
    {
        var validationErrors = ValidateNewUserInput(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail(validationErrors);
        }

        var email = dto.Email.Trim();
        var newUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            PhoneNumber = dto.PhoneNumber?.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(newUser, dto.Password!);
        if (!createResult.Succeeded)
        {
            return ServiceResult<AddCompanyUserResultDto>.Fail(
                "USER_CREATION_FAILED",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var claimsResult = await AddProfileClaimsAsync(newUser, dto.FirstName!, dto.LastName!);
        if (!claimsResult.Success)
        {
            await _userManager.DeleteAsync(newUser);
            return ServiceResult<AddCompanyUserResultDto>.Fail("USER_CLAIMS_FAILED", claimsResult.ErrorMessage ?? "Failed to store user profile data.");
        }

        if (dto.Role == ECompanyRole.Owner)
        {
            var roleResult = await _userManager.AddToRoleAsync(newUser, CompanyOwnerRole);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(newUser);
                return ServiceResult<AddCompanyUserResultDto>.Fail(
                    "ROLE_ASSIGNMENT_FAILED",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        try
        {
            var membership = await _companiesModuleApi.UpsertCompanyMembershipAsync(new UpsertCompanyMembershipContract
            {
                CompanyId = companyId,
                UserId = newUser.Id,
                Role = dto.Role.ToString()
            });

            await _auditService.LogMutationAsync(
                companyId,
                ownerUserName,
                nameof(AppUserCompany),
                membership.Membership.MembershipId,
                "UserAddedToCompany",
                $"{{\"targetUserId\":\"{newUser.Id}\",\"email\":\"{newUser.Email}\",\"role\":\"{dto.Role}\"}}");

            return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
                companyId,
                membership.Membership.MembershipId,
                newUser.Id,
                newUser.Email ?? email,
                dto.Role,
                isExistingUser: false,
                accessStatus: "Immediate access",
                nextAction: "User can log in immediately with the password defined by the company owner."));
        }
        catch (DbUpdateException)
        {
            await _userManager.DeleteAsync(newUser);
            return ServiceResult<AddCompanyUserResultDto>.Fail(
                "MEMBERSHIP_PERSIST_FAILED",
                "Membership could not be created. User creation was rolled back.");
        }
    }

    private static AddCompanyUserResultDto BuildAddResult(
        Guid companyId,
        Guid membershipId,
        Guid userId,
        string email,
        ECompanyRole role,
        bool isExistingUser,
        string accessStatus,
        string nextAction,
        bool membershipReactivated = false,
        bool membershipAlreadyActive = false)
    {
        return BllDtoFactory.CreateAddCompanyUserResultDto(
            companyId,
            membershipId,
            userId,
            email,
            role,
            isExistingUser,
            accessStatus,
            nextAction,
            membershipReactivated,
            membershipAlreadyActive);
    }

    private static List<ServiceError> ValidateNewUserInput(AddCompanyUserRequestDto dto)
    {
        var errors = new List<ServiceError>();
        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Password is required for new users." });
        }

        if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Password confirmation does not match." });
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "First name is required for new users." });
        }

        if (string.IsNullOrWhiteSpace(dto.LastName))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Last name is required for new users." });
        }

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Phone number is required for new users." });
        }

        return errors;
    }

    private async Task<(bool Success, string? ErrorMessage)> EnsureOwnerRoleClaimAsync(AppUser user)
    {
        if (await _userManager.IsInRoleAsync(user, CompanyOwnerRole))
        {
            return (true, null);
        }

        var result = await _userManager.AddToRoleAsync(user, CompanyOwnerRole);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<(bool Success, string? ErrorMessage)> RemoveOwnerRoleIfNoMembershipsAsync(AppUser user)
    {
        var hasAnyOwnerMembership = await _companiesModuleApi.HasAnyActiveOwnerMembershipForUserAsync(user.Id);

        if (hasAnyOwnerMembership || !await _userManager.IsInRoleAsync(user, CompanyOwnerRole))
        {
            return (true, null);
        }

        var result = await _userManager.RemoveFromRoleAsync(user, CompanyOwnerRole);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<(bool Success, string? ErrorMessage)> AddProfileClaimsAsync(AppUser user, string firstName, string lastName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.GivenName, firstName.Trim()),
            new(ClaimTypes.Surname, lastName.Trim())
        };

        var addResult = await _userManager.AddClaimsAsync(user, claims);
        return addResult.Succeeded
            ? (true, null)
            : (false, string.Join(", ", addResult.Errors.Select(e => e.Description)));
    }

    private async Task<ServiceResult> PreventLastOwnerChangeAsync(Guid companyId)
    {
        var activeOwnerCount = await _companiesModuleApi.CountActiveCompanyOwnersAsync(companyId);

        return activeOwnerCount <= 1
            ? ServiceResult.Fail("LAST_OWNER_PROTECTION", "Cannot remove or demote the last active owner.")
            : ServiceResult.Ok();
    }
}
