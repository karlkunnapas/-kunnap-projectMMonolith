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
[Route("api/v{version:apiVersion}/company/{companyId:guid}/promotion")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class PromotionController : ControllerBase
{
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public PromotionController(ICompaniesModuleApi companiesModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
    }

    /// <summary>
    /// List all promotions for the company.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PromotionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<PromotionResponse>>> GetPromotions(Guid companyId)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.GetCompanyPromotionsAsync(companyId);

        return Ok(result.Select(ApiDtoFactory.CreateDto).ToList());
    }

    /// <summary>
    /// Get a specific promotion.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PromotionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionResponse>> GetPromotion(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.GetCompanyPromotionAsync(companyId, id);
        if (result == null)
        {
            return BadRequest(new Message("Promotion not found."));
        }

        return Ok(ApiDtoFactory.CreateDto(result));
    }

    /// <summary>
    /// Create a promotion.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PromotionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionResponse>> CreatePromotion(Guid companyId, [FromBody] PromotionUpsert request)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.CreateCompanyPromotionAsync(companyId, new UpsertCompanyPromotionContract
        {
            Code = request.Code,
            DiscountValue = request.DiscountValue,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            IsActive = request.IsActive
        });

        if (!result.Success || result.Promotion == null)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to create promotion."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Promotion));
    }

    /// <summary>
    /// Update a promotion.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PromotionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionResponse>> UpdatePromotion(Guid companyId, Guid id, [FromBody] PromotionUpsert request)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.UpdateCompanyPromotionAsync(companyId, id, new UpsertCompanyPromotionContract
        {
            Code = request.Code,
            DiscountValue = request.DiscountValue,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            IsActive = request.IsActive
        });

        if (!result.Success || result.Promotion == null)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to update promotion."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Promotion));
    }

    /// <summary>
    /// Delete a promotion.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePromotion(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.DeleteCompanyPromotionAsync(companyId, id);
        if (!result)
        {
            return BadRequest(new Message("Unable to delete promotion."));
        }

        return Ok();
    }
}
