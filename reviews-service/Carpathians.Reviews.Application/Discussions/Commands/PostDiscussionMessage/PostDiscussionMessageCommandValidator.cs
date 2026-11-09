using Carpathians.Reviews.Application.Common.Validation;
using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Discussions.Commands.PostDiscussionMessage;

public class PostDiscussionMessageCommandValidator : AbstractValidator<PostDiscussionMessageCommand>
{
    public PostDiscussionMessageCommandValidator()
    {
        RuleFor(x => x.DiscussionId).MustBeObjectId();
        this.AuthorRules(x => x.AuthorId, x => x.AuthorName);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(DiscussionMessage.MaxTextLength);
        RuleFor(x => x.ReplyTo).MustBeObjectIdIfPresent();
        RuleFor(x => x.Attachments!)
            .Must(a => a.Count <= DiscussionMessage.MaxAttachments)
            .WithMessage($"A message can contain at most {DiscussionMessage.MaxAttachments} attachments")
            .When(x => x.Attachments is not null);
        RuleForEach(x => x.Attachments).NotEmpty().MaximumLength(ValidationRules.MaxUrlLength);
    }
}
