using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;
using Carpathians.Reviews.Domain.Exceptions;
using MongoDB.Bson;
using NSubstitute;

namespace Carpathians.Reviews.Tests.Application;

[TestFixture]
public class ReviewQueryHandlerTests
{
    private IReviewQueries _queries = null!;

    [SetUp]
    public void SetUp() => _queries = Substitute.For<IReviewQueries>();

    [Test]
    public void GetReviewById_NotFound_ThrowsNotFoundException()
    {
        _queries.GetByIdAsync(Arg.Any<ObjectId>(), Arg.Any<CancellationToken>())
            .Returns((ReviewDetailsDto?)null);
        var handler = new GetReviewByIdQueryHandler(_queries);

        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetReviewByIdQuery(ObjectId.GenerateNewId().ToString()), CancellationToken.None));
    }

    [Test]
    public async Task GetReviewsList_ClampsPageSizeAndPassesFilter()
    {
        _queries.GetListAsync(Arg.Any<ReviewFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ReviewDto>(Array.Empty<ReviewDto>(), 0, 1, 100));
        var handler = new GetReviewsListQueryHandler(_queries);

        await handler.Handle(new GetReviewsListQuery(RouteId: 2, MinRating: 4, SortBy: "Rating", PageSize: 1000),
            CancellationToken.None);

        await _queries.Received(1).GetListAsync(
            Arg.Is<ReviewFilter>(f => f.RouteId == 2 && f.MinRating == 4 && f.SortBy == "rating"),
            1, GetReviewsListQuery.MaxPageSize, Arg.Any<CancellationToken>());
    }
}
