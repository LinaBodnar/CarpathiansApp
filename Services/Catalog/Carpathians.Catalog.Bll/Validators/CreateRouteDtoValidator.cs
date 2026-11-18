using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class CreateRouteDtoValidator : AbstractValidator<CreateRouteDto>
{
    public CreateRouteDtoValidator()
    {
        RuleFor(x => x.Slug)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must consist of lowercase alphanumeric characters separated by single hyphens (e.g. 'chornohora-traverse').");

        RuleFor(x => x)
            .Must(x => x.DurationHours <= 0 || (x.DistanceKm / x.DurationHours) <= 15.0m)
            .WithName(nameof(CreateRouteDto.DurationHours))
            .WithMessage("Average hiking speed cannot exceed 15 km/h for a mountain route.")
            .Must(x => x.DurationHours <= 0 || (x.DistanceKm / x.DurationHours) >= 0.2m)
            .WithName(nameof(CreateRouteDto.DurationHours))
            .WithMessage("Estimated hiking speed cannot be less than 0.2 km/h.");

        RuleFor(x => x.ElevationGainM)
            .LessThanOrEqualTo(2061)
            .WithMessage("Elevation gain cannot exceed the highest elevation in Ukraine (2061 m at Hoverla).");

        When(x => x.Detail != null, () =>
        {
            RuleFor(x => x.Detail!).SetValidator(new SetRouteDetailDtoValidator());
        });
    }
}
