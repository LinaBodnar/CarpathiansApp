using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Discussions.Queries.GetRouteDiscussions;
using Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;
using Carpathians.Reviews.Application.Reviews.Queries.GetRouteRating;
using Carpathians.Reviews.Application.Reviews.Queries.GetTopRatedRoutes;
using Carpathians.Reviews.WebApi.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Reviews.WebApi.Controllers;

/// <summary>Дані контексту Відгуків у розрізі маршрутів (маршрутами володіє Каталог).</summary>
[Route("api/routes")]
public class RoutesController : ApiControllerBase
{
    /// <summary>Рейтинг маршруту: середня оцінка, кількість, розподіл 1–5 (aggregation pipeline).</summary>
    [HttpGet("{routeId:long}/rating")]
    [ProducesResponseType(typeof(RouteRatingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteRatingDto>> GetRating(long routeId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetRouteRatingQuery(routeId), ct));

    /// <summary>Найкраще оцінені маршрути.</summary>
    [HttpGet("top-rated")]
    [ProducesResponseType(typeof(IReadOnlyList<RouteRatingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RouteRatingDto>>> GetTopRated(
        [FromQuery] int top = 5, [FromQuery] int minReviews = 1, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetTopRatedRoutesQuery(top, minReviews), ct));

    /// <summary>Обговорення маршруту, найактивніші першими.</summary>
    [HttpGet("{routeId:long}/discussions")]
    [ProducesResponseType(typeof(PagedResult<DiscussionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DiscussionDto>>> GetDiscussions(
        long routeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetRouteDiscussionsQuery(routeId, page, pageSize), ct));

    /// <summary>
    /// Синхронізація денормалізованої назви маршруту (reviews.routeName, discussions.routeName).
    /// Імітує обробку події «маршрут перейменовано» від Каталогу (механізм подій — Лаб. 08).
    /// </summary>
    [HttpPut("{routeId:long}/name")]
    [ProducesResponseType(typeof(UpdatedCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UpdatedCountResponse>> SyncRouteName(
        long routeId, SyncRouteNameRequest request, CancellationToken ct)
        => Ok(new UpdatedCountResponse(await Mediator.Send(new SyncRouteNameCommand(routeId, request.RouteName), ct)));
}
