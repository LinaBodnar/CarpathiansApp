using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;

public class SyncRouteNameCommandValidator : AbstractValidator<SyncRouteNameCommand>
{
    public SyncRouteNameCommandValidator()
    {
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.RouteName).NotEmpty().MaximumLength(Review.MaxRouteNameLength);
    }
}
