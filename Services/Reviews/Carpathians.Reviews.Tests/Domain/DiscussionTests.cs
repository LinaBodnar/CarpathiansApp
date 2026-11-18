using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Tests.Domain;

[TestFixture]
public class DiscussionTests
{
    private static readonly Author Maria = new("3", "Марія");

    private static Discussion CreateDiscussion()
        => new(2, "Заросляк – Несамовите – Говерла", "Скільки йти до озера?", Maria);

    [Test]
    public void PostMessage_UpdatesCounterAndKeepsOnlyLastThreeMessages()
    {
        var discussion = CreateDiscussion();

        for (var i = 1; i <= 5; i++)
            discussion.PostMessage(Maria, $"Повідомлення {i}");

        Assert.Multiple(() =>
        {
            Assert.That(discussion.MessageCount, Is.EqualTo(5));
            Assert.That(discussion.LastMessages, Has.Count.EqualTo(Discussion.LastMessagesLimit));
            Assert.That(discussion.LastMessages.Last().Text, Is.EqualTo("Повідомлення 5"));
            Assert.That(discussion.LastMessages.First().Text, Is.EqualTo("Повідомлення 3"));
        });
    }

    [Test]
    public void PostMessage_ReturnsMessageReferencingDiscussion()
    {
        var discussion = CreateDiscussion();

        var message = discussion.PostMessage(Maria, "Привіт");

        Assert.That(message.DiscussionId, Is.EqualTo(discussion.Id));
    }

    [Test]
    public void PostMessage_ReplyFromAnotherDiscussion_ThrowsDomainException()
    {
        var first = CreateDiscussion();
        var second = CreateDiscussion();
        var foreignMessage = second.PostMessage(Maria, "Чуже повідомлення");

        Assert.That(() => first.PostMessage(Maria, "Відповідь", foreignMessage),
            Throws.TypeOf<DomainException>());
    }

    [Test]
    public void Constructor_EmptyTitle_ThrowsDomainException()
    {
        Assert.That(() => new Discussion(2, "Route", "", Maria), Throws.TypeOf<DomainException>());
    }
}
