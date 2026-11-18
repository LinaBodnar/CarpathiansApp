using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandHandler(IReviewRepository reviews, IRatingService ratings)
    : IRequestHandler<UpdateReviewCommand, int>
{
    public async Task<int> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw NotFoundException.For("Review", cmd.Id);

        if (cmd.ExpectedVersion is not null && cmd.ExpectedVersion != review.Version)
            throw new ConflictException(
                $"Review {cmd.Id} was modified: expected version {cmd.ExpectedVersion}, actual {review.Version}");

        var ratingChanged = review.Rating != cmd.Rating;

        review.Update(cmd.Rating, cmd.Text);
        if (cmd.Tags is not null)
            review.SetTags(cmd.Tags);

        await reviews.UpdateAsync(review, ct);          // атомарна перевірка версії в БД

        if (ratingChanged)
            await ratings.RecalculateAsync(review.RouteId, ct);

        return review.Version;
    }
}
