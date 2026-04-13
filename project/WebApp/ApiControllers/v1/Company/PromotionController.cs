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
[Route("api/v{version:apiVersion}/company/{companyId:guid}/promotion")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class PromotionController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly AppDbContext _context;

    public PromotionController(IPromotionService promotionService, AppDbContext context)
    {
        _promotionService = promotionService;
        _context = context;
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
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _promotionService.GetCompanyPromotionsAsync(companyId);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data?.Select(MapPromotion).ToList() ?? new List<PromotionResponse>());
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
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _promotionService.GetCompanyPromotionAsync(companyId, id);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapPromotion(result.Data));
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
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _promotionService.CreateCompanyPromotionAsync(companyId, new PromotionUpsertDto
        {
            Code = request.Code,
            DiscountValue = request.DiscountValue,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            IsActive = request.IsActive
        });

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapPromotion(result.Data));
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
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _promotionService.UpdateCompanyPromotionAsync(companyId, id, new PromotionUpsertDto
        {
            Code = request.Code,
            DiscountValue = request.DiscountValue,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            IsActive = request.IsActive
        });

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapPromotion(result.Data));
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
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _promotionService.DeleteCompanyPromotionAsync(companyId, id);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok();
    }

    private async Task<bool> IsCompanyMemberAsync(Guid companyId, Guid userId, ECompanyRole minRole = ECompanyRole.Manager)
    {
        return await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive && uc.Role >= minRole);
    }

    private static PromotionResponse MapPromotion(PromotionSummaryDto dto)
    {
        return new PromotionResponse
        {
            Id = dto.Id,
            Code = dto.Code,
            DiscountValue = dto.DiscountValue,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            IsActive = dto.IsActive
        };
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
