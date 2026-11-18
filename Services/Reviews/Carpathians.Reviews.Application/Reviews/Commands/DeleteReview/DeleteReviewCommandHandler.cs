using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;

public class DeleteReviewCommandHandler(IReviewRepository reviews, IRatingService ratings)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        var id = ObjectId.Parse(cmd.Id);
        var review = await reviews.GetByIdAsync(id, ct)
                     ?? throw NotFoundException.For("Review", cmd.Id);

        if (!await reviews.DeleteAsync(id, cmd.ExpectedVersion, ct))
            throw NotFoundException.For("Review", cmd.Id);

        await ratings.RecalculateAsync(review.RouteId, ct);
    }
}
