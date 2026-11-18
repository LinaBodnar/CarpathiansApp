using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using static Carpathians.Reviews.Infrastructure.Seeding.SeedData;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>Обговорення (discussions) і повна гілка повідомлень (discussionMessages).</summary>
public class DiscussionSeeder(MongoDbContext context, ILogger<DiscussionSeeder> logger) : IDataSeeder
{
    public int Order => 1;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Discussions.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;                                                    // уже наповнено

        var messages = new List<DiscussionMessage>();

        var poles = new Discussion(Route1, Route1Name, Discussion1Title, Olena);
        var question = poles.PostMessage(Olena, "Чи потрібні палиці на Говерлу?");
        messages.Add(question);
        messages.Add(poles.PostMessage(Andrii, "Для спуску дуже допомагають.", replyTo: question));
        messages.Add(poles.PostMessage(Maria, "Я ходила без них, але з палицями було б легше.", replyTo: question));
        messages.Add(poles.PostMessage(Olena, "Дякую, візьму."));

        var lake = new Discussion(Route2, Route2Name, Discussion2Title, Maria);
        var lakeQuestion = lake.PostMessage(Maria, "Скільки часу займає захід до озера Несамовите?");
        messages.Add(lakeQuestion);
        messages.Add(lake.PostMessage(Andrii, "Приблизно годину в один бік від основної стежки.", replyTo: lakeQuestion));

        await context.Discussions.InsertManyAsync(new[] { poles, lake }, cancellationToken: ct);
        await context.DiscussionMessages.InsertManyAsync(messages, cancellationToken: ct);

        logger.LogInformation("Seeded {Discussions} discussions and {Messages} messages", 2, messages.Count);
    }
}
