using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class AddGearItemDtoValidator : AbstractValidator<AddGearItemDto>
{
    public AddGearItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .Matches(@"^[\p{L}\p{N}\s\-\(\)\.,']+$")
            .WithMessage("Gear item name contains invalid characters.");
    }
}
