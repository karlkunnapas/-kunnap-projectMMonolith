using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.DTO.v1.Station;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Helpers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/station")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
public class StationController : ControllerBase
{
    private readonly IHomePageService _homePageService;
    private readonly IReservationService _reservationService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly IUnitOfWork _unitOfWork;

    public StationController(
        IHomePageService homePageService,
        IReservationService reservationService,
        IAvailabilityService availabilityService,
        IMaintenanceService maintenanceService,
        IUnitOfWork unitOfWork)
    {
        _homePageService = homePageService;
        _reservationService = reservationService;
        _availabilityService = availabilityService;
        _maintenanceService = maintenanceService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// List all charging stations available to customers.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<StationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<StationSummary>>> GetStations()
    {
        var result = await _homePageService.GetCustomerHomePageAsync();
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var response = result.Data.Stations.Select(s => new StationSummary
        {
            Id = s.Id,
            Name = s.Name,
            NameTranslations = s.NameTranslations,
            Location = s.Location,
            Status = s.Status.ToString(),
            PricePerKwh = s.PricePerKwh,
            MaxPower = s.MaxPower,
            ConnectorNames = s.ConnectorNames,
            IsCompatibleWithSelectedVehicle = s.IsCompatibleWithSelectedVehicle
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Get full details for a specific station including availability.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(StationDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDetails>> GetStation(Guid id, [FromQuery] DateTime? dateUtc = null)
    {
        var result = await _reservationService.GetStationDetailsAsync(id, dateUtc);
        if (!result.Success || result.Data == null)
        {
            return NotFound(new Message("Station not found."));
        }

        var dto = result.Data;
        return Ok(new StationDetails
        {
            Id = dto.Id,
            CompanyId = dto.CompanyId,
            Name = dto.Name,
            Location = dto.Location,
            Status = dto.Status.ToString(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Connectors = dto.Connectors.Select(c => new ConnectorDetail
            {
                ConnectorId = c.ConnectorId,
                Name = c.Name,
                Quantity = c.Quantity,
                AvailableQuantity = c.AvailableQuantity,
                Reservations = c.Reservations.Select(r => new TimeRange
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc
                }).ToList()
            }).ToList(),
            ExistingReservations = dto.ExistingReservations.Select(r => new StationReservationSlot
            {
                StartTimeUtc = r.StartTimeUtc,
                EndTimeUtc = r.EndTimeUtc,
                Status = r.Status.ToString()
            }).ToList(),
            AvailableSlots = dto.AvailableSlots.Select(s => new AvailabilitySlot
            {
                StartTimeUtc = s.StartTimeUtc,
                EndTimeUtc = s.EndTimeUtc,
                IsAvailable = s.IsAvailable
            }).ToList()
        });
    }

    /// <summary>
    /// Get available booking slots for a date and duration.
    /// </summary>
    [HttpGet("{stationId:guid}/slots")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(List<AvailabilitySlot>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AvailabilitySlot>>> GetAvailableSlots(
        Guid stationId,
        [FromQuery] DateTime dateUtc,
        [FromQuery] int durationMinutes = 60)
    {
        var result = await _availabilityService.GetAvailableSlotsAsync(stationId, dateUtc, durationMinutes);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data.Select(s => new AvailabilitySlot
        {
            StartTimeUtc = s.StartTimeUtc,
            EndTimeUtc = s.EndTimeUtc,
            IsAvailable = s.IsAvailable
        }).ToList());
    }

    /// <summary>
    /// Estimate charging cost for a given duration at a station.
    /// </summary>
    [HttpGet("{stationId:guid}/estimate")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(CostEstimate), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CostEstimate>> EstimateCost(
        Guid stationId,
        [FromQuery] int durationMinutes,
        [FromQuery] decimal? estimatedKwh = null)
    {
        var result = await _reservationService.EstimateCostAsync(stationId, durationMinutes, estimatedKwh);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(new CostEstimate
        {
            EstimatedCost = result.Data.EstimatedCost,
            DurationMinutes = result.Data.DurationMinutes
        });
    }

    /// <summary>
    /// Report a fault or issue at a station.
    /// </summary>
    [HttpPost("{stationId:guid}/report-issue")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportIssue(Guid stationId, [FromBody] ReportIssueRequest request)
    {
        var userId = User.UserId();
        var stationResult = await _reservationService.GetStationDetailsAsync(stationId);
        if (!stationResult.Success
            || stationResult.Data?.CompanyId == null
            || stationResult.Data.CompanyId == Guid.Empty)
        {
            return NotFound(new Message("Station not found."));
        }

        var result = await _maintenanceService.CreateMaintenanceAsync(
            stationId,
            stationResult.Data.CompanyId.Value,
            userId,
            request.IssueDescription);

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok();
    }

    /// <summary>
    /// List all available connector types.
    /// </summary>
    [HttpGet("connectors")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(List<ConnectorOption>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ConnectorOption>>> GetConnectors()
    {
        var connectors = await _unitOfWork.Connectors
            .GetQueryable()
            .Where(c => c.IsActive)
            .ToListAsync();

        var response = connectors
            .Select(c => new ConnectorOption
            {
                Id = c.Id,
                Name = c.Name.Translate() ?? c.Name.ToString() ?? string.Empty
            })
            .OrderBy(c => c.Name)
            .ToList();

        return Ok(response);
    }
}
