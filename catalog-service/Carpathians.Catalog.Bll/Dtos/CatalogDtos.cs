using System.ComponentModel.DataAnnotations;
using Carpathians.Domain.Enums;

namespace Carpathians.Bll.Dtos;

public record RegionDto(int Id, string Name, string? Description);

public record CreateRegionDto(
    [property: Required(ErrorMessage = "Region name is required.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Region name must be between 2 and 100 characters.")]
    string Name,

    [property: StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description);

public record UpdateRegionDto(
    [property: Required(ErrorMessage = "Region name is required.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Region name must be between 2 and 100 characters.")]
    string Name,

    [property: StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description);

public record RouteDetailDto(
    string Description,
    string? Season,
    int? MaxAltitudeM,
    string? StartPoint,
    string? Requirements);

public record SetRouteDetailDto(
    [property: Required(ErrorMessage = "Description is required.")]
    [property: StringLength(4000, MinimumLength = 5, ErrorMessage = "Description must be between 5 and 4000 characters.")]
    string Description,

    [property: StringLength(100, ErrorMessage = "Season description cannot exceed 100 characters.")]
    string? Season,

    [property: Range(0, 3000, ErrorMessage = "Max altitude must be between 0 and 3000 meters.")]
    int? MaxAltitudeM,

    [property: StringLength(200, ErrorMessage = "Start point cannot exceed 200 characters.")]
    string? StartPoint,

    [property: StringLength(2000, ErrorMessage = "Requirements cannot exceed 2000 characters.")]
    string? Requirements);

public record GearItemDto(long Id, string Name, bool IsRequired, string? Notes);

public record AddGearItemDto(
    [property: Required(ErrorMessage = "Gear item name is required.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Gear item name must be between 2 and 200 characters.")]
    string Name,

    bool IsRequired,

    [property: StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    string? Notes);

public record GalleryImageDto(
    long Id,
    long PointId,
    string FileName,
    string Url,
    string? Caption,
    int SortOrder,
    DateTime UploadedAt);

public record AddGalleryImageDto(
    [property: Required(ErrorMessage = "File name is required.")]
    [property: StringLength(260, MinimumLength = 3, ErrorMessage = "File name must be between 3 and 260 characters.")]
    string FileName,

    [property: Required(ErrorMessage = "URL is required.")]
    [property: Url(ErrorMessage = "URL must be a valid URL.")]
    [property: StringLength(1000, ErrorMessage = "URL cannot exceed 1000 characters.")]
    string Url,

    [property: StringLength(500, ErrorMessage = "Caption cannot exceed 500 characters.")]
    string? Caption,

    [property: Range(0, 10000, ErrorMessage = "Sort order must be non-negative.")]
    int SortOrder);

public record MapPointDto(
    long Id,
    string Name,
    decimal Latitude,
    decimal Longitude,
    int? AltitudeM,
    PointType PointType,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<GalleryImageDto>? GalleryImages);

public record CreatePointDto(
    [property: Required(ErrorMessage = "Point name is required.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Point name must be between 2 and 200 characters.")]
    string Name,

    [property: Required(ErrorMessage = "Latitude is required.")]
    [property: Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    decimal Latitude,

    [property: Required(ErrorMessage = "Longitude is required.")]
    [property: Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    decimal Longitude,

    [property: Required(ErrorMessage = "Point type is required.")]
    [property: EnumDataType(typeof(PointType), ErrorMessage = "Invalid point type.")]
    PointType PointType,

    int? AltitudeM);

public record UpdatePointDto(
    [property: Required(ErrorMessage = "Point name is required.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Point name must be between 2 and 200 characters.")]
    string Name,

    [property: Required(ErrorMessage = "Latitude is required.")]
    [property: Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    decimal Latitude,

    [property: Required(ErrorMessage = "Longitude is required.")]
    [property: Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    decimal Longitude,

    [property: Required(ErrorMessage = "Point type is required.")]
    [property: EnumDataType(typeof(PointType), ErrorMessage = "Invalid point type.")]
    PointType PointType,

    int? AltitudeM);

public record RoutePointDto(
    long RouteId,
    long PointId,
    int SequenceNo,
    DateTime AddedAt,
    MapPointDto? Point);

public record AddRoutePointDto(
    [property: Required(ErrorMessage = "Point ID is required.")]
    [property: Range(1, long.MaxValue, ErrorMessage = "Point ID must be greater than 0.")]
    long PointId,

    [property: Required(ErrorMessage = "Sequence number is required.")]
    [property: Range(1, 10000, ErrorMessage = "Sequence number must be at least 1.")]
    int SequenceNo);

public record RouteDto(
    long Id,
    int RegionId,
    string RegionName,
    string Name,
    string Slug,
    Difficulty Difficulty,
    decimal DistanceKm,
    decimal DurationHours,
    int ElevationGainM,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    RouteDetailDto? Detail,
    IReadOnlyList<GearItemDto> GearItems,
    IReadOnlyList<RoutePointDto> RoutePoints);

public record CreateRouteDto(
    [property: Required(ErrorMessage = "Region ID is required.")]
    [property: Range(1, int.MaxValue, ErrorMessage = "Region ID must be greater than 0.")]
    int RegionId,

    [property: Required(ErrorMessage = "Route name is required.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Route name must be between 3 and 200 characters.")]
    string Name,

    [property: Required(ErrorMessage = "Slug is required.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Slug must be between 3 and 200 characters.")]
    string Slug,

    [property: Required(ErrorMessage = "Difficulty is required.")]
    [property: EnumDataType(typeof(Difficulty), ErrorMessage = "Invalid difficulty value.")]
    Difficulty Difficulty,

    [property: Required(ErrorMessage = "Distance is required.")]
    [property: Range(0.1, 1000.0, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    decimal DistanceKm,

    [property: Required(ErrorMessage = "Duration is required.")]
    [property: Range(0.1, 200.0, ErrorMessage = "Duration must be between 0.1 and 200 hours.")]
    decimal DurationHours,

    [property: Range(0, 10000, ErrorMessage = "Elevation gain cannot be negative.")]
    int ElevationGainM,

    [property: StringLength(100, ErrorMessage = "Created by string cannot exceed 100 characters.")]
    string? CreatedBy,

    SetRouteDetailDto? Detail);

public record UpdateRouteDto(
    [property: Required(ErrorMessage = "Route name is required.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Route name must be between 3 and 200 characters.")]
    string Name,

    [property: Required(ErrorMessage = "Difficulty is required.")]
    [property: EnumDataType(typeof(Difficulty), ErrorMessage = "Invalid difficulty value.")]
    Difficulty Difficulty,

    [property: Required(ErrorMessage = "Distance is required.")]
    [property: Range(0.1, 1000.0, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    decimal DistanceKm,

    [property: Required(ErrorMessage = "Duration is required.")]
    [property: Range(0.1, 200.0, ErrorMessage = "Duration must be between 0.1 and 200 hours.")]
    decimal DurationHours,

    [property: Range(0, 10000, ErrorMessage = "Elevation gain cannot be negative.")]
    int ElevationGainM,

    [property: StringLength(100, ErrorMessage = "Updated by string cannot exceed 100 characters.")]
    string? UpdatedBy);
