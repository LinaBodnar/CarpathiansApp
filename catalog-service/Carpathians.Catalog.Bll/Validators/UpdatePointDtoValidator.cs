using Carpathians.Bll.Dtos;
using Carpathians.Domain.Enums;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class UpdatePointDtoValidator : AbstractValidator<UpdatePointDto>
{
    public UpdatePointDtoValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(47.5m, 49.5m)
            .WithMessage("Latitude must be within the Ukrainian Carpathian region (47.5° to 49.5° N).");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(22.0m, 26.0m)
            .WithMessage("Longitude must be within the Ukrainian Carpathian region (22.0° to 26.0° E).");

        When(x => x.PointType == PointType.Peak, () =>
        {
            RuleFor(x => x.AltitudeM)
                .NotNull()
                .WithMessage("Altitude is mandatory for Peak points.")
                .InclusiveBetween(400, 2061)
                .WithMessage("Carpathian peak altitude must be between 400 m and 2061 m.");
        });
    }
}
