using Carpathians.Reviews.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence;

/// <summary>
/// Точка доступу до бази <c>ReviewContext</c>. Реєструється як Singleton:
/// MongoClient потокобезпечний і сам керує пулом з'єднань.
/// </summary>
public class MongoDbContext
{
    public const string ReviewsCollection = "reviews";
    public const string DiscussionsCollection = "discussions";
    public const string DiscussionMessagesCollection = "discussionMessages";
    public const string RatingsCollection = "ratings";

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is not configured");

        Client = new MongoClient(settings.ConnectionString);
        Database = Client.GetDatabase(settings.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    // Колекції доменних сутностей (запис через репозиторії)
    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);
    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);
    public IMongoCollection<DiscussionMessage> DiscussionMessages
        => Database.GetCollection<DiscussionMessage>(DiscussionMessagesCollection);

    // Ті самі колекції як BsonDocument — для читальної сторони (проєкції, aggregation pipeline)
    public IMongoCollection<BsonDocument> ReviewDocuments => Database.GetCollection<BsonDocument>(ReviewsCollection);
    public IMongoCollection<BsonDocument> DiscussionDocuments
        => Database.GetCollection<BsonDocument>(DiscussionsCollection);
    public IMongoCollection<BsonDocument> DiscussionMessageDocuments
        => Database.GetCollection<BsonDocument>(DiscussionMessagesCollection);
    public IMongoCollection<BsonDocument> Ratings => Database.GetCollection<BsonDocument>(RatingsCollection);
}
