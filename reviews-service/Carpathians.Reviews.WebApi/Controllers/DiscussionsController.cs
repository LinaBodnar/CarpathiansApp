using AutoMapper;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;
using Carpathians.Reviews.Application.Discussions.Commands.PostDiscussionMessage;
using Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionById;
using Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionMessages;
using Carpathians.Reviews.WebApi.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Reviews.WebApi.Controllers;

/// <summary>Обговорення маршрутів (discussions + discussionMessages).</summary>
[Route("api/discussions")]
public class DiscussionsController(IMapper mapper) : ApiControllerBase
{
    /// <summary>Обговорення з лічильником і останніми повідомленнями (вбудованими).</summary>
    [HttpGet("{id}", Name = nameof(GetDiscussionById))]
    [ProducesResponseType(typeof(DiscussionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscussionDto>> GetDiscussionById(string id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetDiscussionByIdQuery(id), ct));

    /// <summary>Нове обговорення з першим повідомленням.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateDiscussionRequest request, CancellationToken ct)
    {
        var id = await Mediator.Send(mapper.Map<CreateDiscussionCommand>(request), ct);
        return CreatedAtRoute(nameof(GetDiscussionById), new { id }, new CreatedResponse(id));
    }

    /// <summary>Повна гілка повідомлень з пагінацією.</summary>
    [HttpGet("{id}/messages")]
    [ProducesResponseType(typeof(PagedResult<DiscussionMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<DiscussionMessageDto>>> GetMessages(
        string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetDiscussionMessagesQuery(id, page, pageSize), ct));

    /// <summary>Нове повідомлення в обговоренні.</summary>
    [HttpPost("{id}/messages")]
    [ProducesResponseType(typeof(CreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedResponse>> PostMessage(
        string id, PostMessageRequest request, CancellationToken ct)
    {
        var messageId = await Mediator.Send(new PostDiscussionMessageCommand(
            id, request.AuthorId, request.AuthorName, request.Text, request.ReplyTo, request.Attachments), ct);

        return CreatedAtRoute(nameof(GetDiscussionById), new { id }, new CreatedResponse(messageId));
    }
}
