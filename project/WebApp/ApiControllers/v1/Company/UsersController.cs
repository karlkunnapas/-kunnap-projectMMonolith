using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.Domain;
using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using WebApp.Helpers;
using WebApp.Mappers;

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
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public UsersController(IIdentityService identityService, ICompaniesModuleApi companiesModuleApi)
    {
        _identityService = identityService;
        _companiesModuleApi = companiesModuleApi;
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

        return Ok(result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<CompanyUserResponse>());
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

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
            ApiDtoFactory.CreateDto(request));

        if (HasNotOwnerOrForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
            ApiDtoFactory.CreateDto(request));

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
        return await _companiesModuleApi.HasActiveOwnerMembershipAsync(companyId, userId);
    }

    private static bool HasNotOwnerOrForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code is "NOT_OWNER" or "FORBIDDEN");
    }
}
