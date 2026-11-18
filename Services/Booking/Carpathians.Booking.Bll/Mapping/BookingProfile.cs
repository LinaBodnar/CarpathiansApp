using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Domain.Models;
namespace Carpathians.Bll.Mapping;

public class BookingProfile : Profile
{
    public BookingProfile()
    {
        CreateMap<Customer, CustomerDto>();
        CreateMap<CustomerInputDto, Customer>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore());

        CreateMap<Departure, DepartureDto>();
        CreateMap<CreateDepartureDto, Departure>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RouteName, o => o.Ignore())
            .ForMember(d => d.SeatsAvailable, o => o.Ignore());

        CreateMap<BookingItem, BookingItemDto>();
        CreateMap<Booking, BookingDto>();
    }
}