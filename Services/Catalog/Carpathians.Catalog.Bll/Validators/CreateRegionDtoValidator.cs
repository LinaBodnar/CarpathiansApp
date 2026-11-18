using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class CreateRegionDtoValidator : AbstractValidator<CreateRegionDto>
{
    public CreateRegionDtoValidator()
    {
        RuleFor(x => x.Name)
            .Matches(@"^[\p{L}\s\-']+$")
            .WithMessage("Region name can only contain letters, spaces, hyphens, and apostrophes.");
    }
}
