using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class UpdateRegionDtoValidator : AbstractValidator<UpdateRegionDto>
{
    public UpdateRegionDtoValidator()
    {
        RuleFor(x => x.Name)
            .Matches(@"^[\p{L}\s\-']+$")
            .WithMessage("Region name can only contain letters, spaces, hyphens, and apostrophes.");
    }
}
