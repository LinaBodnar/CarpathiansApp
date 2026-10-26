using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class AddRoutePointDtoValidator : AbstractValidator<AddRoutePointDto>
{
    public AddRoutePointDtoValidator()
    {
        RuleFor(x => x.SequenceNo)
            .GreaterThan(0)
            .WithMessage("Sequence number must be greater than 0.");
    }
}
