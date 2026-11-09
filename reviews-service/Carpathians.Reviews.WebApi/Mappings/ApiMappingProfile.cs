using AutoMapper;
using Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.WebApi.Contracts;

namespace Carpathians.Reviews.WebApi.Mappings;

/// <summary>Мапінг контрактів API у команди MediatR (вкладені об'єкти й масиви — автоматично).</summary>
public class ApiMappingProfile : Profile
{
    public ApiMappingProfile()
    {
        CreateMap<GeoPointRequest, GeoPointInput>();
        CreateMap<WeatherRequest, WeatherInput>();
        CreateMap<CreateReviewRequest, CreateReviewCommand>();
        CreateMap<CreateDiscussionRequest, CreateDiscussionCommand>();
    }
}
