using AutoMapper;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Reviews.Commands.AddReviewComment;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;
using Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsFeed;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;
using Carpathians.Reviews.WebApi.Contracts;
using Carpathians.Reviews.WebApi.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Reviews.WebApi.Controllers;

/// <summary>Відгуки про маршрути (колекція reviews).</summary>
[Route("api/reviews")]
public class ReviewsController(IMapper mapper) : ApiControllerBase
{
    /// <summary>Список відгуків з фільтрацією, сортуванням і пагінацією (skip/limit).</summary>
    /// <param name="routeId">Відгуки одного маршруту.</param>
    /// <param name="authorId">Відгуки одного автора.</param>
    /// <param name="minRating">Мінімальна оцінка (1–5).</param>
    /// <param name="tag">Тег (елемент масиву tags).</param>
    /// <param name="search">Повнотекстовий пошук по тексту відгуку.</param>
    /// <param name="sortBy">createdAt | rating.</param>
    /// <param name="sortDir">asc | desc.</param>
    /// <param name="page">Номер сторінки з 1.</param>
    /// <param name="pageSize">Розмір сторінки (1–100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? routeId, [FromQuery] string? authorId, [FromQuery] int? minRating,
        [FromQuery] string? tag, [FromQuery] string? search,
        [FromQuery] string sortBy = "createdAt", [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetReviewsListQuery(routeId, authorId, minRating, tag, search, sortBy, sortDir, page, pageSize), ct);

        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Стрічка відгуків з курсорною пагінацією: передайте nextCursor як after.</summary>
    [HttpGet("feed")]
    [ProducesResponseType(typeof(CursorPage<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CursorPage<ReviewDto>>> GetFeed(
        [FromQuery] long? routeId, [FromQuery] string? after, [FromQuery] int limit = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetReviewsFeedQuery(routeId, after, limit), ct));

    /// <summary>Повний відгук. Версія документа повертається в заголовку ETag.</summary>
    [HttpGet("{id}", Name = nameof(GetReviewById))]
    [ProducesResponseType(typeof(ReviewDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDetailsDto>> GetReviewById(string id, CancellationToken ct)
    {
        var review = await Mediator.Send(new GetReviewByIdQuery(id), ct);
        Response.SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Створення відгуку. Один відгук від автора на маршрут.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var id = await Mediator.Send(mapper.Map<CreateReviewCommand>(request), ct);
        Response.SetETag(0);
        return CreatedAtRoute(nameof(GetReviewById), new { id }, new CreatedResponse(id));
    }

    /// <summary>Редагування відгуку. If-Match: "версія" — захист від втрати одночасних змін.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, UpdateReviewRequest request, CancellationToken ct)
    {
        var command = new UpdateReviewCommand(id, request.Rating, request.Text, request.Tags,
            Request.GetIfMatchVersion());

        var newVersion = await Mediator.Send(command, ct);
        Response.SetETag(newVersion);
        return NoContent();
    }

    /// <summary>Видалення відгуку (If-Match — необов'язково).</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id, Request.GetIfMatchVersion()), ct);
        return NoContent();
    }

    /// <summary>Коментар до відгуку (вбудований масив comments).</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(ReviewDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDetailsDto>> AddComment(
        string id, AddCommentRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(
            new AddReviewCommentCommand(id, request.AuthorId, request.AuthorName, request.Text), ct);
        Response.SetETag(review.Version);
        return Ok(review);
    }
}
