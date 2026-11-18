using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;
using Carpathians.Reviews.Application.Reviews.Queries.GetRouteRating;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;

namespace Carpathians.Reviews.WebApi.Grpc;

/// <summary>
/// gRPC-API сервісу відгуків (контракт Protos/reviews.proto) для внутрішніх викликів (Aggregator).
/// Як і REST-контролери, надсилає запити CQRS через MediatR — валідація (ValidationBehavior),
/// логування й кеш (CachedReviewQueries) працюють однаково для обох протоколів.
/// <para>Помилки: некоректний запит — INVALID_ARGUMENT тут або з валідаторів FluentValidation;
/// доменні винятки перетворює <see cref="ReviewsGrpcExceptionInterceptor"/>.</para>
/// </summary>
public sealed class ReviewsGrpcService(ISender mediator) : ReviewsGrpc.ReviewsGrpcBase
{
    public const int MaxBatchSize = 100;
    public const int MaxLatestReviews = 20;

    public override async Task<RouteRatingReply> GetRouteRating(GetRouteRatingRequest request, ServerCallContext context)
    {
        EnsurePositive(request.RouteId);
        var rating = await mediator.Send(new GetRouteRatingQuery(request.RouteId), context.CancellationToken);
        return ToReply(rating);
    }

    public override async Task<RouteRatingsReply> GetRatingsForRoutes(GetRatingsForRoutesRequest request, ServerCallContext context)
    {
        if (request.RouteIds.Count == 0 || request.RouteIds.Count > MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"From 1 to {MaxBatchSize} route ids are expected"));
        foreach (var id in request.RouteIds)
            EnsurePositive(id);

        var reply = new RouteRatingsReply();
        foreach (var routeId in request.RouteIds.Distinct())
        {
            try
            {
                reply.Ratings.Add(ToReply(await mediator.Send(new GetRouteRatingQuery(routeId), context.CancellationToken)));
            }
            catch (NotFoundException)
            {
                // маршрут ще без відгуків — пропускається
            }
        }

        return reply;
    }

    public override async Task<ReviewsReply> GetLatestReviews(GetLatestReviewsRequest request, ServerCallContext context)
    {
        EnsurePositive(request.RouteId);
        if (request.Count is < 1 or > MaxLatestReviews)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Count must be between 1 and {MaxLatestReviews}"));

        var page = await mediator.Send(new GetReviewsListQuery(
            RouteId: request.RouteId, SortBy: "createdAt", SortDir: "desc", Page: 1, PageSize: request.Count),
            context.CancellationToken);

        var reply = new ReviewsReply();
        reply.Reviews.AddRange(page.Items.Select(ToReply));
        return reply;
    }

    private static RouteRatingReply ToReply(RouteRatingDto rating)
    {
        var reply = new RouteRatingReply
        {
            RouteId = rating.RouteId,
            RouteName = rating.RouteName,
            AvgRating = rating.AvgRating,
            ReviewsCount = rating.Count
        };
        foreach (var (score, count) in rating.Distribution)
            reply.Distribution[score] = count;
        return reply;
    }

    private static ReviewReply ToReply(ReviewDto review) => new()
    {
        Id = review.Id,
        RouteId = review.RouteId,
        AuthorName = review.AuthorName,
        Rating = review.Rating,
        Text = review.Text,
        CommentsCount = review.CommentsCount,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc))
    };

    private static void EnsurePositive(long routeId)
    {
        if (routeId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Route id must be positive"));
    }
}
