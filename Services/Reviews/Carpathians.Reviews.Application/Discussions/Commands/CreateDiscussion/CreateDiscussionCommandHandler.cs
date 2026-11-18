using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;

public class CreateDiscussionCommandHandler(IDiscussionRepository discussions, IReviewRepository reviews)
    : IRequestHandler<CreateDiscussionCommand, string>
{
    public async Task<string> Handle(CreateDiscussionCommand cmd, CancellationToken ct)
    {
        Review? review = null;
        if (!string.IsNullOrWhiteSpace(cmd.ReviewId))
        {
            review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
                     ?? throw NotFoundException.For("Review", cmd.ReviewId);
            if (review.RouteId != cmd.RouteId)
                throw new DomainException("Review belongs to another route");
            if (review.DiscussionId is not null)
                throw new ConflictException($"Review {cmd.ReviewId} already has a discussion");
        }

        var author = new Author(cmd.AuthorId, cmd.AuthorName);
        var discussion = new Discussion(cmd.RouteId, cmd.RouteName, cmd.Title, author);
        var firstMessage = discussion.PostMessage(author, cmd.Message);

        await discussions.AddAsync(discussion, ct);
        await discussions.AddMessageAsync(discussion, firstMessage, ct);

        if (review is not null)
        {
            review.LinkDiscussion(discussion.Id);       // reference reviews → discussions
            await reviews.UpdateAsync(review, ct);
        }

        return discussion.Id.ToString();
    }
}
