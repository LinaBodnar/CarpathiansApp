using Carpathians.Bll.Dtos;
using FluentValidation;

namespace Carpathians.Bll.Validators;

public class AddGalleryImageDtoValidator : AbstractValidator<AddGalleryImageDto>
{
    public AddGalleryImageDtoValidator()
    {
        RuleFor(x => x.FileName)
            .Must(fileName =>
            {
                var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
                return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
            })
            .WithMessage("Image file must have a valid extension (.jpg, .jpeg, .png, .webp).");

        RuleFor(x => x.Url)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithMessage("URL must be a valid HTTP or HTTPS address.");
    }
}
