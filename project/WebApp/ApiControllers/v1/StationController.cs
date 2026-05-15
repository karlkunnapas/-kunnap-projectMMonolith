using App.DTO.v1.Station;
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
[Route("api/v{version:apiVersion}/station")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
public class StationController : ControllerBase
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public StationController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
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
        var stations = await _chargingModuleApi.GetStationsForHomeAsync();
        var response = stations.Select(ApiDtoFactory.CreateDto).ToList();

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
        var station = await _chargingModuleApi.GetStationByIdAsync(id);
        if (station == null)
        {
            return NotFound(new Message("Station not found."));
        }

        var selectedDate = (dateUtc ?? DateTime.UtcNow).Date;
        var stationReservations = await _chargingModuleApi.GetStationReservationsAsync(id);
        var activeReservations = stationReservations
            .Where(r => r.Status is EReservationStatus.Active or EReservationStatus.Started)
            .Where(r => r.EndTimeUtc > DateTime.UtcNow)
            .OrderBy(r => r.StartTimeUtc)
            .ToList();

        var details = new StationDetails
        {
            Id = station.Id,
            CompanyId = station.CompanyId,
            Name = station.Name,
            Location = station.Location,
            Status = station.Status.ToString(),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Connectors = station.Connectors
                .Select(c => new ConnectorDetail
                {
                    ConnectorId = c.Id,
                    Name = c.Name,
                    Quantity = 1,
                    AvailableQuantity = activeReservations.Any() ? 0 : 1,
                    Reservations = activeReservations.Select(r => new TimeRange
                    {
                        StartTimeUtc = r.StartTimeUtc,
                        EndTimeUtc = r.EndTimeUtc
                    }).ToList()
                })
                .ToList(),
            ExistingReservations = activeReservations
                .Select(r => new StationReservationSlot
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    Status = r.Status.ToString()
                })
                .ToList(),
            AvailableSlots = BuildAvailabilitySlots(activeReservations, selectedDate, 60)
                .Select(x => new AvailabilitySlot
                {
                    StartTimeUtc = x.StartTimeUtc,
                    EndTimeUtc = x.EndTimeUtc,
                    IsAvailable = x.IsAvailable
                })
                .ToList()
        };

        return Ok(details);
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
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return NotFound(new Message("Station not found."));
        }

        var stationReservations = await _chargingModuleApi.GetStationReservationsAsync(stationId);
        var activeReservations = stationReservations
            .Where(r => r.Status is EReservationStatus.Active or EReservationStatus.Started)
            .Where(r => r.EndTimeUtc > DateTime.UtcNow)
            .ToList();
        var slots = BuildAvailabilitySlots(activeReservations, dateUtc.Date, durationMinutes)
            .Select(x => new AvailabilitySlot
            {
                StartTimeUtc = x.StartTimeUtc,
                EndTimeUtc = x.EndTimeUtc,
                IsAvailable = x.IsAvailable
            })
            .ToList();

        return Ok(slots);
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
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return NotFound(new Message("Station not found."));
        }

        var estimate = new CostEstimate
        {
            DurationMinutes = durationMinutes,
            EstimatedCost = CalculateEstimatedCost(station.PricePerKwh, durationMinutes, estimatedKwh, station.MaxPower)
        };
        return Ok(estimate);
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
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station?.CompanyId == null || station.CompanyId == Guid.Empty)
        {
            return NotFound(new Message("Station not found."));
        }

        var result = await _chargingModuleApi.CreateMaintenanceAsync(new MaintenanceContract
        {
            Id = Guid.NewGuid(),
            CompanyId = station.CompanyId.Value,
            ChargingStationId = stationId,
            StationName = station.Name,
            ReportedByUserId = userId,
            IssueDescription = request.IssueDescription,
            Status = EMaintenanceStatus.Reported,
            ReportedAtUtc = DateTime.UtcNow,
            ResolvedAtUtc = null,
            AssignedToUserId = null,
            Notes = null
        }, actorUserName: User.Identity?.Name ?? userId.ToString());
        if (result == null)
        {
            return BadRequest(new Message("Unable to report issue."));
        }

        await _companiesModuleApi.LogAuditMutationAsync(
            station.CompanyId.Value,
            User.Identity?.Name ?? userId.ToString(),
            nameof(MaintenanceContract),
            result.Id,
            "IssueReported",
            $"StationId={stationId};Description={request.IssueDescription}");

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
        var connectorsFromModule = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);

        var response = connectorsFromModule
            .Select(ApiDtoFactory.CreateDtoForStationOption)
            .OrderBy(c => c.Name)
            .ToList();

        return Ok(response);
    }

    private static decimal CalculateEstimatedCost(decimal pricePerKwh, int durationMinutes, decimal? estimatedKwh, decimal stationMaxPower)
    {
        if (durationMinutes <= 0)
        {
            return 0m;
        }

        var energy = estimatedKwh
            ?? Math.Round((durationMinutes / 60m) * Math.Max(1m, Math.Min(stationMaxPower, 200m)) * 0.6m, 2, MidpointRounding.AwayFromZero);
        if (energy < 0m)
        {
            energy = 0m;
        }

        return Math.Round(pricePerKwh * energy, 2, MidpointRounding.AwayFromZero);
    }

    private static List<AvailabilitySlot> BuildAvailabilitySlots(
        IReadOnlyCollection<ReservationContract> activeReservations,
        DateTime dayUtc,
        int durationMinutes)
    {
        var slotDuration = Math.Max(5, durationMinutes);
        var start = dayUtc.Date;
        var end = start.AddDays(1);
        var slots = new List<AvailabilitySlot>();

        for (var cursor = start; cursor < end; cursor = cursor.AddMinutes(slotDuration))
        {
            var slotEnd = cursor.AddMinutes(slotDuration);
            var overlap = activeReservations.Any(r => r.StartTimeUtc < slotEnd && cursor < r.EndTimeUtc);
            slots.Add(new AvailabilitySlot
            {
                StartTimeUtc = cursor,
                EndTimeUtc = slotEnd,
                IsAvailable = !overlap
            });
        }

        return slots;
    }
}
