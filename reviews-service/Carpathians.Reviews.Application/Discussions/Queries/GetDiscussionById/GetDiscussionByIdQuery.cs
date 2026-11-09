using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionById;

public record GetDiscussionByIdQuery(string Id) : IQuery<DiscussionDto>;
