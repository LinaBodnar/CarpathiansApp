using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Exceptions;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionById;

public class GetDiscussionByIdQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionByIdQuery, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(GetDiscussionByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(ObjectId.Parse(q.Id), ct)
           ?? throw NotFoundException.For("Discussion", q.Id);
}
