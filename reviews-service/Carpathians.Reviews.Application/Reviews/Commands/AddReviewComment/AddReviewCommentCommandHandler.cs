using AutoMapper;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Reviews.Commands.AddReviewComment;

public class AddReviewCommentCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<AddReviewCommentCommand, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(AddReviewCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
                     ?? throw NotFoundException.For("Review", cmd.ReviewId);

        review.AddComment(new Author(cmd.AuthorId, cmd.AuthorName), cmd.Text);
        await reviews.UpdateAsync(review, ct);

        return mapper.Map<ReviewDetailsDto>(review);
    }
}
