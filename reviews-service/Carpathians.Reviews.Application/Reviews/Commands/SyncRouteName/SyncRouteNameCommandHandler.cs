using Carpathians.Reviews.Domain.Interfaces;
using MediatR;

namespace Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;

public class SyncRouteNameCommandHandler(IReviewRepository reviews, IDiscussionRepository discussions)
    : IRequestHandler<SyncRouteNameCommand, long>
{
    public async Task<long> Handle(SyncRouteNameCommand cmd, CancellationToken ct)
    {
        var updatedReviews = await reviews.UpdateRouteNameAsync(cmd.RouteId, cmd.RouteName, ct);
        var updatedDiscussions = await discussions.UpdateRouteNameAsync(cmd.RouteId, cmd.RouteName, ct);
        return updatedReviews + updatedDiscussions;
    }
}
