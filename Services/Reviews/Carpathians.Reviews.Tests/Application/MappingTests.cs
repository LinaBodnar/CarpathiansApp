using AutoMapper;
using Carpathians.Reviews.Application.Common.Mappings;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.ValueObjects;
using Carpathians.Reviews.WebApi.Contracts;
using Carpathians.Reviews.WebApi.Mappings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Carpathians.Reviews.Tests.Application;

[TestFixture]
public class MappingTests
{
    private MapperConfiguration _configuration = null!;
    private IMapper _mapper = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ReviewMappingProfile>();
            cfg.AddProfile<ApiMappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = _configuration.CreateMapper();
    }

    [Test]
    public void Configuration_IsValid() => _configuration.AssertConfigurationIsValid();

    [Test]
    public void Review_MapsToDetailsDto_WithObjectIdAsStringAndFlattenedAuthor()
    {
        var review = new Review(1, "Заросляк – Говерла", new Author("1", "Олена"), 5, "Чудово");
        review.AddComment(new Author("2", "Андрій"), "Згоден");
        review.SetWeather(new WeatherInfo(8, "strong", null));

        var dto = _mapper.Map<ReviewDetailsDto>(review);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(review.Id.ToString()));
            Assert.That(dto.AuthorId, Is.EqualTo("1"));
            Assert.That(dto.AuthorName, Is.EqualTo("Олена"));
            Assert.That(dto.Comments.Single().AuthorName, Is.EqualTo("Андрій"));
            Assert.That(dto.Weather!.Wind, Is.EqualTo("strong"));
            Assert.That(dto.DiscussionId, Is.Null);
        });
    }

    [Test]
    public void CreateReviewRequest_MapsToCommand_WithNestedObjects()
    {
        var request = new CreateReviewRequest
        {
            RouteId = 2, RouteName = "Route", AuthorId = "3", AuthorName = "Марія", Rating = 5, Text = "Text",
            Tags = new List<string> { "lake" },
            GpsTrack = new List<GeoPointRequest> { new() { Lat = 48.1, Lng = 24.5 }, new() { Lat = 48.2, Lng = 24.6, Ele = 2000 } },
            Weather = new WeatherRequest { TempC = 10 }
        };

        var command = _mapper.Map<CreateReviewCommand>(request);

        Assert.Multiple(() =>
        {
            Assert.That(command.RouteId, Is.EqualTo(2));
            Assert.That(command.GpsTrack, Has.Count.EqualTo(2));
            Assert.That(command.GpsTrack![1].Ele, Is.EqualTo(2000));
            Assert.That(command.Weather!.TempC, Is.EqualTo(10));
            Assert.That(command.Tags, Is.EqualTo(new[] { "lake" }));
        });
    }
}
