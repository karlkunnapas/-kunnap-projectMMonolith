using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Services;

public class IdentityService : IIdentityService
{
    private const string CustomerRole = "Customer";
    private const string CompanyOwnerRole = "CompanyOwner";

    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _context;

    public IdentityService(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        AppDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _context = context;
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
}