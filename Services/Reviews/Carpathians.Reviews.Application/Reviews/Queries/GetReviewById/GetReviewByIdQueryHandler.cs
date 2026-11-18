using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Exceptions;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;

public class GetReviewByIdQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewByIdQuery, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(GetReviewByIdQuery query, CancellationToken ct)
        => await queries.GetByIdAsync(ObjectId.Parse(query.Id), ct)
           ?? throw NotFoundException.For("Review", query.Id);
}
