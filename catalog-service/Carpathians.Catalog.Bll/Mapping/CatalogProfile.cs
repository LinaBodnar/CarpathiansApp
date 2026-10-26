using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Domain.Entities;

namespace Carpathians.Bll.Mapping;

public class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        CreateMap<Region, RegionDto>();
        CreateMap<RouteDetail, RouteDetailDto>();
        CreateMap<GearItem, GearItemDto>();
        CreateMap<GalleryImage, GalleryImageDto>();
        CreateMap<MapPoint, MapPointDto>();
        CreateMap<RoutePoint, RoutePointDto>();

        CreateMap<TrailRoute, RouteDto>()
            .ForMember(dest => dest.RegionName, opt => opt.MapFrom(src => src.Region != null ? src.Region.Name : string.Empty));
    }
}
