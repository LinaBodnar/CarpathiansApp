using AutoMapper;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Application.Common.Mappings;

/// <summary>
/// Мапінг доменних сутностей і Value Objects у DTO:
/// ObjectId → string, Author → AuthorId/AuthorName, вкладені документи й масиви.
/// </summary>
public class ReviewMappingProfile : Profile
{
    public ReviewMappingProfile()
    {
        CreateMap<Comment, CommentDto>()
            .ForCtorParam(nameof(CommentDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(CommentDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName));

        CreateMap<GeoPoint, GeoPointDto>();
        CreateMap<WeatherInfo, WeatherDto>();

        CreateMap<Review, ReviewDto>()
            .ForCtorParam(nameof(ReviewDto.Id), o => o.MapFrom(s => s.Id.ToString()))
            .ForCtorParam(nameof(ReviewDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(ReviewDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName))
            .ForCtorParam(nameof(ReviewDto.CommentsCount), o => o.MapFrom(s => s.Comments.Count));

        CreateMap<Review, ReviewDetailsDto>()
            .ForCtorParam(nameof(ReviewDetailsDto.Id), o => o.MapFrom(s => s.Id.ToString()))
            .ForCtorParam(nameof(ReviewDetailsDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(ReviewDetailsDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName))
            .ForCtorParam(nameof(ReviewDetailsDto.DiscussionId),
                o => o.MapFrom(s => s.DiscussionId.HasValue ? s.DiscussionId.Value.ToString() : null));

        CreateMap<MessagePreview, MessagePreviewDto>()
            .ForCtorParam(nameof(MessagePreviewDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(MessagePreviewDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName));

        CreateMap<Discussion, DiscussionDto>()
            .ForCtorParam(nameof(DiscussionDto.Id), o => o.MapFrom(s => s.Id.ToString()))
            .ForCtorParam(nameof(DiscussionDto.CreatedById), o => o.MapFrom(s => s.CreatedBy.UserId))
            .ForCtorParam(nameof(DiscussionDto.CreatedByName), o => o.MapFrom(s => s.CreatedBy.DisplayName));

        CreateMap<DiscussionMessage, DiscussionMessageDto>()
            .ForCtorParam(nameof(DiscussionMessageDto.Id), o => o.MapFrom(s => s.Id.ToString()))
            .ForCtorParam(nameof(DiscussionMessageDto.DiscussionId), o => o.MapFrom(s => s.DiscussionId.ToString()))
            .ForCtorParam(nameof(DiscussionMessageDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(DiscussionMessageDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName))
            .ForCtorParam(nameof(DiscussionMessageDto.ReplyTo),
                o => o.MapFrom(s => s.ReplyTo.HasValue ? s.ReplyTo.Value.ToString() : null));
    }
}
