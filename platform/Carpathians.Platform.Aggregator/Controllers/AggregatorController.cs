using Carpathians.Platform.Aggregator.Models;
using Carpathians.Platform.Aggregator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Platform.Aggregator.Controllers;

/// <summary>Агрегувальні ендпоінти: одна відповідь з даними кількох мікросервісів.</summary>
[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController(AggregationService aggregation) : ControllerBase
{
    /// <summary>
    /// Повна картка бронювання: бронювання й виїзди (booking-api) + маршрути (catalog-api)
    /// + рейтинг і свіжі відгуки (reviews-api). Якщо catalog-api чи reviews-api недоступні —
    /// 200 з частковими даними й переліком Warnings.
    /// </summary>
    [HttpGet("bookings/{id:long}/full")]
    [ProducesResponseType(typeof(FullBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullBookingDto>> GetFullBooking(long id, CancellationToken ct)
    {
        try
        {
            var result = await aggregation.GetFullBookingAsync(id, ct);
            return result is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Booking not found",
                    detail: $"Booking {id} was not found")
                : Ok(result);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable",
                detail: $"{ex.Service} is unavailable, the booking card cannot be built");
        }
    }

    /// <summary>Огляд маршруту: catalog-api і reviews-api паралельно.</summary>
    [HttpGet("routes/{routeId:long}/overview")]
    [ProducesResponseType(typeof(RouteOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteOverviewDto>> GetRouteOverview(long routeId, CancellationToken ct)
    {
        var result = await aggregation.GetRouteOverviewAsync(routeId, ct);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Route not found",
                detail: $"Route {routeId} was not found")
            : Ok(result);
    }
}
