using Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsFeed;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;
using FluentValidation.TestHelper;

namespace Carpathians.Reviews.Tests.Application;

[TestFixture]
public class QueryValidatorTests
{
    [TestCase("not-an-object-id")]
    [TestCase("66f0c20000000000000000")]
    [TestCase("")]
    public void GetReviewById_InvalidObjectId_HasError(string id)
    {
        var result = new GetReviewByIdQueryValidator().TestValidate(new GetReviewByIdQuery(id));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Test]
    public void GetReviewById_ValidObjectId_NoErrors()
    {
        var result = new GetReviewByIdQueryValidator()
            .TestValidate(new GetReviewByIdQuery("66f0c2000000000000000001"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void GetReviewsList_UnknownSortField_HasError()
    {
        var result = new GetReviewsListQueryValidator()
            .TestValidate(new GetReviewsListQuery(SortBy: "text"));
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Test]
    public void GetReviewsList_PageSizeTooLarge_HasError()
    {
        var result = new GetReviewsListQueryValidator()
            .TestValidate(new GetReviewsListQuery(PageSize: 500));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Test]
    public void GetReviewsFeed_InvalidCursor_HasError()
    {
        var result = new GetReviewsFeedQueryValidator()
            .TestValidate(new GetReviewsFeedQuery(After: "xyz"));
        result.ShouldHaveValidationErrorFor(x => x.After);
    }
}
