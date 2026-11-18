using Carpathians.Reviews.Application.Common.Models;
using MongoDB.Bson;

namespace Carpathians.Reviews.Infrastructure.Persistence.Queries;

/// <summary>Перетворення документів (результатів проєкцій) у DTO читальної сторони.</summary>
internal static class ReviewReadMapper
{
    /// <summary>Поля, які вибираються з БД для списку відгуків (без comments, photos, gpsTrack).</summary>
    public static BsonDocument ListProjection() => new()
    {
        { "routeId", 1 },
        { "routeName", 1 },
        { "author", 1 },
        { "rating", 1 },
        { "text", 1 },
        { "tags", 1 },
        { "isVerifiedParticipant", 1 },
        { "createdAt", 1 },
        { "updatedAt", 1 },
        { "version", 1 },
        { "commentsCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() })) }
    };

    public static ReviewDto ToReviewDto(BsonDocument d)
    {
        var author = d.Doc("author");
        return new ReviewDto(
            d["_id"].AsObjectId.ToString(),
            d.Int64("routeId"),
            d.Str("routeName"),
            author.Str("userId"),
            author.Str("displayName"),
            d.Int32("rating"),
            d.Str("text"),
            d.Int32("commentsCount"),
            d.Strings("tags"),
            d.BoolOrNull("isVerifiedParticipant"),
            d.Date("createdAt"),
            d.DateOrNull("updatedAt"),
            d.Int32("version"));
    }

    public static ReviewDetailsDto ToReviewDetailsDto(BsonDocument d)
    {
        var author = d.Doc("author");
        var weather = d.DocOrNull("weather");

        return new ReviewDetailsDto(
            d["_id"].AsObjectId.ToString(),
            d.Int64("routeId"),
            d.Str("routeName"),
            author.Str("userId"),
            author.Str("displayName"),
            d.Int32("rating"),
            d.Str("text"),
            d.Strings("photos"),
            d.Strings("tags"),
            d.Docs("comments").Select(c =>
            {
                var ca = c.Doc("author");
                return new CommentDto(ca.Str("userId"), ca.Str("displayName"), c.Str("text"), c.Date("createdAt"));
            }).ToList(),
            d.Docs("gpsTrack").Select(p =>
                new GeoPointDto(p.Double("lat"), p.Double("lng"), p.DoubleOrNull("ele"))).ToList(),
            weather is null
                ? null
                : new WeatherDto(weather.DoubleOrNull("tempC"), weather.StrOrNull("wind"), weather.StrOrNull("conditions")),
            d.IdOrNull("discussionId"),
            d.BoolOrNull("isVerifiedParticipant"),
            d.Date("createdAt"),
            d.DateOrNull("updatedAt"),
            d.Int32("version"));
    }

    public static RouteRatingDto ToRouteRatingDto(BsonDocument d)
    {
        var distribution = d.Doc("distribution").Elements
            .Where(e => e.Value.IsNumeric)
            .ToDictionary(e => e.Name, e => e.Value.ToInt32());

        return new RouteRatingDto(
            d.Int64("routeId"),
            d.StrOrNull("routeName"),
            d.Double("avgRating"),
            d.Int32("count"),
            distribution);
    }

    public static MessagePreviewDto ToMessagePreviewDto(BsonDocument m)
    {
        var author = m.Doc("author");
        return new MessagePreviewDto(author.Str("userId"), author.Str("displayName"), m.Str("text"), m.Date("createdAt"));
    }

    public static DiscussionDto ToDiscussionDto(BsonDocument d)
    {
        var createdBy = d.Doc("createdBy");
        return new DiscussionDto(
            d["_id"].AsObjectId.ToString(),
            d.Int64("routeId"),
            d.Str("routeName"),
            d.Str("title"),
            createdBy.Str("userId"),
            createdBy.Str("displayName"),
            d.Int32("messageCount"),
            d.Docs("lastMessages").Select(ToMessagePreviewDto).ToList(),
            d.Date("createdAt"),
            d.Date("lastActivityAt"));
    }

    public static DiscussionMessageDto ToDiscussionMessageDto(BsonDocument d)
    {
        var author = d.Doc("author");
        return new DiscussionMessageDto(
            d["_id"].AsObjectId.ToString(),
            d.IdOrNull("discussionId") ?? string.Empty,
            author.Str("userId"),
            author.Str("displayName"),
            d.Str("text"),
            d.IdOrNull("replyTo"),
            d.Strings("attachments"),
            d.Date("createdAt"));
    }
}
