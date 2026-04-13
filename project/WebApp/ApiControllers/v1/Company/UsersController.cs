using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Helpers;

namespace WebApp.ApiControllers.v1.Company;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/company/{companyId:guid}/users")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class UsersController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly AppDbContext _context;

    public UsersController(IIdentityService identityService, AppDbContext context)
    {
        _identityService = identityService;
        _context = context;
    }

    /// <summary>
    /// List all members of the company.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CompanyUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CompanyUserResponse>>> GetUsers(Guid companyId)
    {
        var userId = User.UserId();
        if (!await IsCompanyOwnerAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _identityService.GetCompanyUsersAsync(companyId, userId);
        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data?.Select(MapMembership).ToList() ?? new List<CompanyUserResponse>());
    }

    /// <summary>
    /// Get a specific membership.
    /// </summary>
    [HttpGet("{membershipId:guid}")]
    [ProducesResponseType(typeof(CompanyUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyUserResponse>> GetUser(Guid companyId, Guid membershipId)
    {
        var userId = User.UserId();
        if (!await IsCompanyOwnerAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _identityService.GetCompanyUserMembershipAsync(companyId, userId, membershipId);
        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapMembership(result.Data));
    }

    /// <summary>
    /// Add a user to the company.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AddCompanyUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AddCompanyUserResponse>> AddUser(Guid companyId, [FromBody] AddCompanyUserRequest request)
    {
        var userId = User.UserId();
        if (!await IsCompanyOwnerAsync(companyId, userId))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(ECompanyRole), request.Role))
        {
            return BadRequest(new Message("Invalid company role."));
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _identityService.AddUserToCompanyAsync(
            companyId,
            userId,
            userName,
            new AddCompanyUserRequestDto
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Password = request.Password,
                ConfirmPassword = request.ConfirmPassword,
                Role = (ECompanyRole)request.Role
            });

        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(new AddCompanyUserResponse
        {
            MembershipId = result.Data.MembershipId,
            UserId = result.Data.UserId,
            Email = result.Data.Email,
            Role = result.Data.Role.ToString(),
            IsExistingUser = result.Data.IsExistingUser,
            AccessStatus = result.Data.AccessStatus,
            NextAction = result.Data.NextAction
        });
    }

    /// <summary>
    /// Update a member's role.
    /// </summary>
    [HttpPut("{membershipId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUserRole(Guid companyId, Guid membershipId, [FromBody] UpdateCompanyUserRole request)
    {
        var userId = User.UserId();
        if (!await IsCompanyOwnerAsync(companyId, userId))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(ECompanyRole), request.Role))
        {
            return BadRequest(new Message("Invalid company role."));
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _identityService.UpdateCompanyUserRoleAsync(
            companyId,
            userId,
            userName,
            membershipId,
            new UpdateCompanyUserRoleRequestDto
            {
                Role = (ECompanyRole)request.Role
            });

        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok();
    }

    /// <summary>
    /// Remove a user from the company.
    /// </summary>
    [HttpDelete("{membershipId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveUser(Guid companyId, Guid membershipId)
    {
        var userId = User.UserId();
        if (!await IsCompanyOwnerAsync(companyId, userId))
        {
            return Forbid();
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _identityService.RemoveCompanyUserAsync(companyId, userId, userName, membershipId);
        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok();
    }

    private async Task<bool> IsCompanyOwnerAsync(Guid companyId, Guid userId)
    {
        return await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive && uc.Role == ECompanyRole.Owner);
    }

    private static CompanyUserResponse MapMembership(CompanyUserMembershipDto dto)
    {
        return new CompanyUserResponse
        {
            MembershipId = dto.MembershipId,
            UserId = dto.UserId,
            Email = dto.Email,
            Role = dto.Role.ToString(),
            IsActive = dto.IsActive,
            JoinedAtUtc = dto.JoinedAtUtc
        };
    }

    private static bool HasNotOwnerOrForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code is "NOT_OWNER" or "FORBIDDEN");
    }
}
