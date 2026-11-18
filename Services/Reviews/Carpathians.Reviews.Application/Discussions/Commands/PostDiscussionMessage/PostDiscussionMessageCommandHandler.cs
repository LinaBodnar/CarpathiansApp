using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Discussions.Commands.PostDiscussionMessage;

public class PostDiscussionMessageCommandHandler(IDiscussionRepository discussions)
    : IRequestHandler<PostDiscussionMessageCommand, string>
{
    public async Task<string> Handle(PostDiscussionMessageCommand cmd, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(ObjectId.Parse(cmd.DiscussionId), ct)
                         ?? throw NotFoundException.For("Discussion", cmd.DiscussionId);

        DiscussionMessage? replyTo = null;
        if (!string.IsNullOrWhiteSpace(cmd.ReplyTo))
        {
            replyTo = await discussions.GetMessageByIdAsync(ObjectId.Parse(cmd.ReplyTo), ct)
                      ?? throw NotFoundException.For("Message", cmd.ReplyTo);
        }

        var message = discussion.PostMessage(new Author(cmd.AuthorId, cmd.AuthorName), cmd.Text, replyTo);
        foreach (var url in cmd.Attachments ?? Array.Empty<string>())
            message.AddAttachment(url);

        await discussions.AddMessageAsync(discussion, message, ct);
        return message.Id.ToString();
    }
}
