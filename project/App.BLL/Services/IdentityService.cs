using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace App.BLL.Services;

public class IdentityService : IIdentityService
{
    private const string CustomerRole = "Customer";
    private const string CompanyOwnerRole = "CompanyOwner";

    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;

    public IdentityService(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        AppDbContext context,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _context = context;
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
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<UserCompanyListResultDto>.Fail("USER_NOT_FOUND", "User not found.");
        }

        // Get all AppUserCompany records for this user across all tenants
        // Need to ignore query filters to see all companies
        var userCompanies = await _context.AppUserCompanies
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
            .Include(uc => uc.Company)
            .ToListAsync();

        var companyDtos = userCompanies.Select(uc => new CompanySelectionItemDto
        {
            MembershipId = uc.Id,
            CompanyId = uc.CompanyId,
            CompanyName = uc.Company?.Name ?? "Unknown",
            CompanySlug = uc.Company?.Slug ?? "",
            Role = uc.Role.ToString()
        }).ToList();

        var result = new UserCompanyListResultDto
        {
            UserId = userId,
            Email = user.Email ?? "",
            Companies = companyDtos
        };

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

        var memberships = await _context.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.CompanyId == companyId)
            .Include(uc => uc.AppUser)
            .OrderByDescending(uc => uc.IsActive)
            .ThenByDescending(uc => uc.JoinedAtUtc)
            .ToListAsync();

        var result = memberships.Select(uc => new CompanyUserMembershipDto
        {
            MembershipId = uc.Id,
            UserId = uc.AppUserId,
            Email = uc.AppUser?.Email ?? string.Empty,
            Role = uc.Role,
            IsActive = uc.IsActive,
            JoinedAtUtc = uc.JoinedAtUtc
        }).ToList();

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

        var membership = await _context.AppUserCompanies
            .AsNoTracking()
            .Include(uc => uc.AppUser)
            .FirstOrDefaultAsync(uc => uc.Id == membershipId && uc.CompanyId == companyId);

        if (membership == null)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("NOT_FOUND", "Membership not found.");
        }

        return ServiceResult<CompanyUserMembershipDto>.Ok(MapMembershipDto(membership));
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

        var membership = await _context.AppUserCompanies
            .Include(uc => uc.AppUser)
            .FirstOrDefaultAsync(uc => uc.Id == membershipId && uc.CompanyId == companyId);

        if (membership == null)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("NOT_FOUND", "Membership not found.");
        }

        if (!membership.IsActive)
        {
            return ServiceResult<CompanyUserMembershipDto>.Fail("MEMBERSHIP_INACTIVE", "Cannot edit inactive membership.");
        }

        if (membership.Role == ECompanyRole.Owner && dto.Role != ECompanyRole.Owner)
        {
            var lastOwnerCheck = await PreventLastOwnerChangeAsync(companyId);
            if (!lastOwnerCheck.Success)
            {
                return ServiceResult<CompanyUserMembershipDto>.Fail(lastOwnerCheck.Errors);
            }
        }

        membership.Role = dto.Role;
        _unitOfWork.AppUserCompanies.Update(membership);
        await _unitOfWork.SaveAsync();

        var targetUser = membership.AppUser;
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
            membership.Id,
            "MembershipRoleUpdated",
            $"{{\"targetUserId\":\"{membership.AppUserId}\",\"role\":\"{membership.Role}\"}}");

        return ServiceResult<CompanyUserMembershipDto>.Ok(MapMembershipDto(membership));
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

        var membership = await _context.AppUserCompanies
            .Include(uc => uc.AppUser)
            .FirstOrDefaultAsync(uc => uc.Id == membershipId && uc.CompanyId == companyId);

        if (membership == null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Membership not found.");
        }

        if (!membership.IsActive)
        {
            return ServiceResult.Fail("MEMBERSHIP_INACTIVE", "Membership is already inactive.");
        }

        if (membership.Role == ECompanyRole.Owner)
        {
            var lastOwnerCheck = await PreventLastOwnerChangeAsync(companyId);
            if (!lastOwnerCheck.Success)
            {
                return lastOwnerCheck;
            }
        }

        membership.IsActive = false;
        _unitOfWork.AppUserCompanies.Update(membership);
        await _unitOfWork.SaveAsync();

        if (membership.AppUser != null)
        {
            var cleanupResult = await RemoveOwnerRoleIfNoMembershipsAsync(membership.AppUser);
            if (!cleanupResult.Success)
            {
                return ServiceResult.Fail("ROLE_ASSIGNMENT_FAILED", cleanupResult.ErrorMessage ?? "Failed to update owner role.");
            }
        }

        await _auditService.LogMutationAsync(
            companyId,
            ownerUserName,
            nameof(AppUserCompany),
            membership.Id,
            "MembershipRemoved",
            $"{{\"targetUserId\":\"{membership.AppUserId}\",\"role\":\"{membership.Role}\"}}");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CompanySelectionItemDto>> SetActiveCompanyAsync(Guid userId, Guid companyId, string actorUserName)
    {
        if (userId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanySelectionItemDto>.Fail("VALIDATION", "User and company identifiers are required.");
        }

        var membership = await _context.AppUserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .FirstOrDefaultAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive);

        if (membership == null || membership.Company == null || !membership.Company.IsActive)
        {
            return ServiceResult<CompanySelectionItemDto>.Fail("FORBIDDEN", "Company selection is not allowed.");
        }

        await _auditService.LogMutationAsync(
            companyId,
            actorUserName,
            nameof(AppUserCompany),
            membership.Id,
            "CompanySwitched",
            $"{{\"userId\":\"{userId}\",\"role\":\"{membership.Role}\"}}");

        return ServiceResult<CompanySelectionItemDto>.Ok(new CompanySelectionItemDto
        {
            MembershipId = membership.Id,
            CompanyId = membership.CompanyId,
            CompanyName = membership.Company.Name.Translate() ?? membership.Company.Name.ToString() ?? string.Empty,
            CompanySlug = membership.Company.Slug,
            Role = membership.Role.ToString()
        });
    }

    public async Task<ServiceResult<Guid>> RegisterCompanyOwnerAsync(RegisterCompanyOwnerDto dto)
    {
        // Check for duplicate company slug
        var existingCompany = await _context.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == dto.CompanySlug.ToLowerInvariant());

        if (existingCompany != null)
        {
            return ServiceResult<Guid>.Fail("DUPLICATE_SLUG", "A company with this slug already exists.");
        }

        // Check for duplicate email
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return ServiceResult<Guid>.Fail("DUPLICATE_EMAIL", "A user with this email already exists.");
        }

        // Create the company
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr(dto.CompanyName),
            ContactEmail = dto.Email,
            ContactPhone = dto.PhoneNumber,
            Slug = dto.CompanySlug.ToLowerInvariant(),
            IsActive = true,
        };

        await _unitOfWork.Companies.AddAsync(company);

        // Save to get company ID assigned
        await _unitOfWork.SaveAsync();

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

        // Create AppUserCompany linking user to company as Owner
        var appUserCompany = new AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = user.Id,
            CompanyId = company.Id,
            Role = ECompanyRole.Owner,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.AppUserCompanies.AddAsync(appUserCompany);
        
        await _unitOfWork.SaveAsync();

        return ServiceResult<Guid>.Ok(company.Id);
    }

    public async Task<ServiceResult<Guid>> RegisterCustomerAsync(RegisterCustomerDto dto)
    {
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return ServiceResult<Guid>.Fail("DUPLICATE_EMAIL", "A user with this email already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult<Guid>.Fail(
                "USER_CREATION_FAILED",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded)
        {
            return ServiceResult<Guid>.Fail(
                "ROLE_ASSIGNMENT_FAILED",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        return ServiceResult<Guid>.Ok(user.Id);
    }

    private async Task<ServiceResult> EnsureOwnerAsync(Guid companyId, Guid ownerUserId)
    {
        var company = await _context.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        if (company == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Company not found.");
        }

        if (!company.IsActive)
        {
            return ServiceResult.Fail("COMPANY_INACTIVE", "Company is inactive.");
        }

        var ownerMembership = await _context.AppUserCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(uc =>
                uc.CompanyId == companyId
                && uc.AppUserId == ownerUserId
                && uc.IsActive
                && uc.Role == ECompanyRole.Owner);

        return ownerMembership == null
            ? ServiceResult.Fail("NOT_OWNER", "Only company owners can manage users.")
            : ServiceResult.Ok();
    }

    private async Task<ServiceResult<AddCompanyUserResultDto>> LinkExistingUserMembershipAsync(
        Guid companyId,
        string ownerUserName,
        AppUser user,
        ECompanyRole role)
    {
        var membership = await _context.AppUserCompanies
            .FirstOrDefaultAsync(uc => uc.CompanyId == companyId && uc.AppUserId == user.Id);

        if (membership != null)
        {
            if (membership.IsActive)
            {
                return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
                    companyId,
                    membership.Id,
                    user.Id,
                    user.Email ?? string.Empty,
                    role,
                    isExistingUser: true,
                    accessStatus: "Already active",
                    nextAction: "User already has active company access.",
                    membershipAlreadyActive: true));
            }

            membership.IsActive = true;
            membership.Role = role;
            membership.JoinedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveAsync();

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
                membership.Id,
                "MembershipReactivated",
                $"{{\"targetUserId\":\"{user.Id}\",\"email\":\"{user.Email}\",\"role\":\"{role}\"}}");

            return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
                companyId,
                membership.Id,
                user.Id,
                user.Email ?? string.Empty,
                role,
                isExistingUser: true,
                accessStatus: "Reactivated",
                nextAction: "Membership was reactivated and access is immediate.",
                membershipReactivated: true));
        }

        var newMembership = new AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = user.Id,
            CompanyId = companyId,
            Role = role,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.AppUserCompanies.AddAsync(newMembership);
        await _unitOfWork.SaveAsync();

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
            newMembership.Id,
            "ExistingUserLinked",
            $"{{\"targetUserId\":\"{user.Id}\",\"email\":\"{user.Email}\",\"role\":\"{role}\"}}");

        return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
            companyId,
            newMembership.Id,
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

        var membership = new AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = newUser.Id,
            CompanyId = companyId,
            Role = dto.Role,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.AppUserCompanies.AddAsync(membership);

        try
        {
            await _unitOfWork.SaveAsync();
        }
        catch (DbUpdateException)
        {
            await _userManager.DeleteAsync(newUser);
            return ServiceResult<AddCompanyUserResultDto>.Fail(
                "MEMBERSHIP_PERSIST_FAILED",
                "Membership could not be created. User creation was rolled back.");
        }

        await _auditService.LogMutationAsync(
            companyId,
            ownerUserName,
            nameof(AppUserCompany),
            membership.Id,
            "UserAddedToCompany",
            $"{{\"targetUserId\":\"{newUser.Id}\",\"email\":\"{newUser.Email}\",\"role\":\"{dto.Role}\"}}");

        return ServiceResult<AddCompanyUserResultDto>.Ok(BuildAddResult(
            companyId,
            membership.Id,
            newUser.Id,
            newUser.Email ?? email,
            dto.Role,
            isExistingUser: false,
            accessStatus: "Immediate access",
            nextAction: "User can log in immediately with the password defined by the company owner."));
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
        return new AddCompanyUserResultDto
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            UserId = userId,
            Email = email,
            Role = role,
            IsExistingUser = isExistingUser,
            AccessStatus = accessStatus,
            NextAction = nextAction,
            MembershipReactivated = membershipReactivated,
            MembershipAlreadyActive = membershipAlreadyActive
        };
    }

    private static CompanyUserMembershipDto MapMembershipDto(AppUserCompany membership)
    {
        return new CompanyUserMembershipDto
        {
            MembershipId = membership.Id,
            UserId = membership.AppUserId,
            Email = membership.AppUser?.Email ?? string.Empty,
            Role = membership.Role,
            IsActive = membership.IsActive,
            JoinedAtUtc = membership.JoinedAtUtc
        };
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
        var hasAnyOwnerMembership = await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == user.Id && uc.IsActive && uc.Role == ECompanyRole.Owner);

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
        var activeOwnerCount = await _context.AppUserCompanies
            .AsNoTracking()
            .CountAsync(uc => uc.CompanyId == companyId && uc.IsActive && uc.Role == ECompanyRole.Owner);

        return activeOwnerCount <= 1
            ? ServiceResult.Fail("LAST_OWNER_PROTECTION", "Cannot remove or demote the last active owner.")
            : ServiceResult.Ok();
    }
}
