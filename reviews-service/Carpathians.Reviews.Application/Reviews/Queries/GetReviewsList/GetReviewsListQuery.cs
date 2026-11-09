using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;

/// <summary>Список відгуків з фільтрацією, сортуванням і пагінацією skip/limit.</summary>
public record GetReviewsListQuery(
    long? RouteId = null,
    string? AuthorId = null,
    int? MinRating = null,
    string? Tag = null,
    string? Search = null,
    string SortBy = "createdAt",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewDto>>
{
    public const int MaxPageSize = 100;
}
