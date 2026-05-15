using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
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
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;

    public UsersController(
        ICompaniesModuleApi companiesModuleApi,
        IUsersModuleApi usersModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
        _usersModuleApi = usersModuleApi;
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Owner"))
        {
            return Forbid();
        }

        var memberships = await _companiesModuleApi.GetCompanyMembershipsAsync(companyId);
        var response = new List<CompanyUserResponse>(memberships.Count);
        foreach (var membership in memberships)
        {
            var displayName = await _usersModuleApi.GetUserDisplayNameAsync(membership.UserId) ?? string.Empty;
            response.Add(new CompanyUserResponse
            {
                MembershipId = membership.MembershipId,
                UserId = membership.UserId,
                Email = displayName,
                Role = membership.Role,
                IsActive = membership.IsActive,
                JoinedAtUtc = membership.JoinedAtUtc
            });
        }

        return Ok(response);
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Owner"))
        {
            return Forbid();
        }

        var membership = await _companiesModuleApi.GetCompanyMembershipAsync(companyId, membershipId);
        if (membership == null)
        {
            return BadRequest(new Message("Membership not found."));
        }

        var displayName = await _usersModuleApi.GetUserDisplayNameAsync(membership.UserId) ?? string.Empty;
        return Ok(new CompanyUserResponse
        {
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            Email = displayName,
            Role = membership.Role,
            IsActive = membership.IsActive,
            JoinedAtUtc = membership.JoinedAtUtc
        });
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Owner"))
        {
            return Forbid();
        }

        if (request.Role < 0 || request.Role > 3)
        {
            return BadRequest(new Message("Invalid company role."));
        }

        var role = request.Role switch
        {
            0 => "Customer",
            1 => "Employee",
            2 => "Manager",
            3 => "Owner",
            _ => string.Empty
        };
        var result = await _companiesModuleApi.AddCompanyUserAsync(new AddCompanyUserContract
        {
            CompanyId = companyId,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Password = request.Password,
            Role = role
        });

        if (!result.Success)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to add company user."));
        }

        return Ok(new AddCompanyUserResponse
        {
            MembershipId = result.MembershipId,
            UserId = result.UserId,
            Email = result.Email,
            Role = result.Role,
            IsExistingUser = result.IsExistingUser,
            AccessStatus = result.AccessStatus,
            NextAction = result.NextAction
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Owner"))
        {
            return Forbid();
        }

        if (request.Role < 0 || request.Role > 3)
        {
            return BadRequest(new Message("Invalid company role."));
        }

        var targetRole = request.Role switch
        {
            0 => "Customer",
            1 => "Employee",
            2 => "Manager",
            3 => "Owner",
            _ => string.Empty
        };
        var result = await _companiesModuleApi.UpdateCompanyMembershipRoleWithGuardsAsync(companyId, membershipId, targetRole);
        if (!result.Success)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to update company user role."));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Owner"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.DeactivateCompanyMembershipWithGuardsAsync(companyId, membershipId);
        if (!result.Success)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to remove company user."));
        }

        return Ok();
    }
}
