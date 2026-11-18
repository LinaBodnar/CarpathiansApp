using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Domain.ValueObjects;
using MediatR;

namespace Carpathians.Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler(IReviewRepository reviews, IRatingService ratings)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        var review = new Review(
            cmd.RouteId,
            cmd.RouteName,
            new Author(cmd.AuthorId, cmd.AuthorName),
            cmd.Rating,
            cmd.Text);

        if (cmd.Photos is { Count: > 0 })
            review.SetPhotos(cmd.Photos);
        if (cmd.Tags is { Count: > 0 })
            review.SetTags(cmd.Tags);
        if (cmd.GpsTrack is { Count: > 0 })
            review.SetGpsTrack(cmd.GpsTrack.Select(p => new GeoPoint(p.Lat, p.Lng, p.Ele)));
        if (cmd.Weather is not null)
            review.SetWeather(new WeatherInfo(cmd.Weather.TempC, cmd.Weather.Wind, cmd.Weather.Conditions));

        await reviews.AddAsync(review, ct);
        await ratings.RecalculateAsync(review.RouteId, ct);

        return review.Id.ToString();
    }
}
