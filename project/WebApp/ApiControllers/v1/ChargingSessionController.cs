using App.DTO.v1.Session;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Helpers;
using WebApp.Mappers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chargingsession")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class ChargingSessionController : ControllerBase
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public ChargingSessionController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    /// <summary>
    /// Get charging session history for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionResponse>>> GetSessions()
    {
        var userId = User.UserId();
        var result = await _chargingModuleApi.GetUserChargingSessionsAsync(userId);
        var response = new List<SessionResponse>(result.Count);
        foreach (var session in result)
        {
            var enriched = await EnrichPromotionAsync(userId, session);
            response.Add(ApiDtoFactory.CreateDto(enriched));
        }
        return Ok(response);
    }

    /// <summary>
    /// Get full details for a specific session.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDetailResponse>> GetSession(Guid id)
    {
        var userId = User.UserId();
        var result = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(id, userId);
        if (result != null)
        {
            var enriched = await EnrichPromotionAsync(userId, result);
            return Ok(ApiDtoFactory.CreateDetailDto(enriched));
        }
        return Forbid();
    }

    /// <summary>
    /// Start a new charging session.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionResponse>> StartSession([FromBody] SessionStartRequest request)
    {
        var userId = User.UserId();
        var reservationId = request.ReservationId ?? Guid.Empty;
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId);
        if (reservation == null)
        {
            return Forbid();
        }
        if (reservation.Status != EReservationStatus.Active)
        {
            return BadRequest(new Message("Unable to start charging session."));
        }
        if (reservation.StartTimeUtc > DateTime.UtcNow || DateTime.UtcNow >= reservation.EndTimeUtc)
        {
            return BadRequest(new Message("Unable to start charging session."));
        }

        var existing = await _chargingModuleApi.GetChargingSessionByReservationIdAsync(reservation.Id);
        if (existing != null)
        {
            return Ok(ApiDtoFactory.CreateDto(existing));
        }

        var created = await _chargingModuleApi.CreateChargingSessionAsync(new ChargingSessionContract
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = reservation.ChargingStationId,
            ReservationId = reservation.Id,
            PromotionId = reservation.PromotionId,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = null,
            EnergyConsumed = 0m,
            Cost = 0m,
            StationName = reservation.StationName
        });

        await _chargingModuleApi.UpdateReservationStatusAsync(
            reservation.Id,
            EReservationStatus.Started,
            reservation.ExpiresAtUtc,
            reservation.CancelledAtUtc,
            EStationStatus.InUse);

        var enrichedCreated = await EnrichPromotionAsync(userId, created);
        return Ok(ApiDtoFactory.CreateDto(enrichedCreated));
    }

    /// <summary>
    /// Stop an active charging session.
    /// </summary>
    [HttpPost("{id:guid}/stop")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionResponse>> StopSession(Guid id, [FromBody] SessionStopRequest request)
    {
        var userId = User.UserId();
        var session = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(id, userId);
        if (session == null)
        {
            return Forbid();
        }
        if (session.EndTimeUtc.HasValue)
        {
            return Ok(ApiDtoFactory.CreateDto(session));
        }

        Guid? reservationPromotionId = session.PromotionId;
        if (!reservationPromotionId.HasValue && session.ReservationId.HasValue)
        {
            var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(session.ReservationId.Value, userId);
            reservationPromotionId = reservation?.PromotionId;
        }

        Guid? selectedPromotionId = reservationPromotionId;
        decimal discountPercent = 0m;

        if (!selectedPromotionId.HasValue && !string.IsNullOrWhiteSpace(request.PromotionCode))
        {
            var selectedPromotion = await _companiesModuleApi.GetValidUserPromotionByCodeAsync(userId, request.PromotionCode.Trim());
            if (selectedPromotion?.Promotion == null || selectedPromotion.IsUsed)
            {
                return BadRequest(new Message("Invalid promotion code."));
            }

            var stationCompanyId = await _chargingModuleApi.GetStationCompanyIdAsync(session.ChargingStationId);
            if (selectedPromotion.Promotion.CompanyId.HasValue && stationCompanyId != selectedPromotion.Promotion.CompanyId)
            {
                return BadRequest(new Message("Selected promotion is not valid for this station company."));
            }

            selectedPromotionId = selectedPromotion.PromotionId;
            discountPercent = selectedPromotion.Promotion.DiscountValue;
        }
        else if (selectedPromotionId.HasValue)
        {
            var wallet = await _companiesModuleApi.GetUserPromotionsAsync(userId);
            discountPercent = wallet.FirstOrDefault(x => x.PromotionId == selectedPromotionId && x.Promotion != null)?.Promotion?.DiscountValue ?? 0m;
        }

        var endTimeUtc = DateTime.UtcNow;
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((endTimeUtc - session.StartTimeUtc).TotalMinutes));
        var energy = Math.Round((durationMinutes / 60m) * Math.Max(1m, Math.Min(session.StationMaxPower ?? 50m, 200m)), 2, MidpointRounding.AwayFromZero);
        var baseCost = Math.Round(energy * session.StationPricePerKwh, 2, MidpointRounding.AwayFromZero);
        var total = Math.Max(0m, Math.Round(baseCost - (baseCost * discountPercent / 100m), 2, MidpointRounding.AwayFromZero));

        var ok = await _chargingModuleApi.CompleteChargingSessionAsync(
            session.Id,
            endTimeUtc,
            energy,
            total,
            selectedPromotionId,
            EStationStatus.Available);
        if (!ok)
        {
            return BadRequest(new Message("Unable to stop charging session."));
        }

        if (selectedPromotionId.HasValue)
        {
            var wallet = await _companiesModuleApi.GetUserPromotionsAsync(userId);
            var selected = wallet.FirstOrDefault(p => p.PromotionId == selectedPromotionId && !p.IsUsed);
            if (selected != null)
            {
                await _companiesModuleApi.RemoveUserPromotionAsync(userId, selected.Id);
            }
        }

        var updated = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(id, userId);
        var enrichedUpdated = await EnrichPromotionAsync(userId, updated ?? session);
        return Ok(ApiDtoFactory.CreateDto(enrichedUpdated));
    }

    private async Task<ChargingSessionContract> EnrichPromotionAsync(Guid userId, ChargingSessionContract session)
    {
        var promotionId = session.PromotionId;
        if (!promotionId.HasValue && session.ReservationId.HasValue)
        {
            var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(session.ReservationId.Value, userId);
            promotionId = reservation?.PromotionId;
        }

        if (!promotionId.HasValue)
        {
            return session;
        }

        var wallet = await _companiesModuleApi.GetUserPromotionsAsync(userId);
        var promotion = wallet.FirstOrDefault(x => x.PromotionId == promotionId.Value)?.Promotion;
        if (promotion == null)
        {
            return session;
        }

        return new ChargingSessionContract
        {
            Id = session.Id,
            UserId = session.UserId,
            ChargingStationId = session.ChargingStationId,
            ReservationId = session.ReservationId,
            PromotionId = promotionId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            EnergyConsumed = session.EnergyConsumed,
            Cost = session.Cost,
            StationName = session.StationName,
            StationPricePerKwh = session.StationPricePerKwh,
            StationMaxPower = session.StationMaxPower,
            PromotionCode = promotion.Code,
            PromotionDiscountValue = promotion.DiscountValue
        };
    }
}
