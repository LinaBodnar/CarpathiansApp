using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class SetRouteDetailDtoValidator : AbstractValidator<SetRouteDetailDto>
{
    public SetRouteDetailDtoValidator()
    {
        When(x => x.MaxAltitudeM.HasValue, () =>
        {
            RuleFor(x => x.MaxAltitudeM!.Value)
                .InclusiveBetween(200, 2061)
                .WithMessage("Carpathian mountain altitudes must be between 200 m (foothills) and 2061 m (Hoverla peak).");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Season), () =>
        {
            RuleFor(x => x.Season!)
                .Matches(@"^[\p{L}\s\-,]+$")
                .WithMessage("Season may only contain letters, spaces, hyphens, and commas.");
        });
    }
}
