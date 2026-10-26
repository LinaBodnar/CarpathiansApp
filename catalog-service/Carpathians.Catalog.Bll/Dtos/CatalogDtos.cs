using Carpathians.Domain.Enums;

namespace Carpathians.Bll.Dtos;

public record RegionDto(int Id, string Name, string? Description);
public record CreateRegionDto(string Name, string? Description);
public record UpdateRegionDto(string Name, string? Description);

public record RouteDetailDto(
    string Description,
    string? Season,
    int? MaxAltitudeM,
    string? StartPoint,
    string? Requirements);

public record SetRouteDetailDto(
    string Description,
    string? Season,
    int? MaxAltitudeM,
    string? StartPoint,
    string? Requirements);

public record GearItemDto(long Id, string Name, bool IsRequired, string? Notes);
public record AddGearItemDto(string Name, bool IsRequired = true, string? Notes = null);

public record GalleryImageDto(
    long Id,
    long PointId,
    string FileName,
    string Url,
    string? Caption,
    int SortOrder,
    DateTime UploadedAt);

public record AddGalleryImageDto(
    string FileName,
    string Url,
    string? Caption,
    int SortOrder = 0);

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
    string Name,
    decimal Latitude,
    decimal Longitude,
    PointType PointType,
    int? AltitudeM = null);

public record UpdatePointDto(
    string Name,
    decimal Latitude,
    decimal Longitude,
    PointType PointType,
    int? AltitudeM = null);

public record RoutePointDto(
    long RouteId,
    long PointId,
    int SequenceNo,
    DateTime AddedAt,
    MapPointDto? Point);

public record AddRoutePointDto(
    long PointId,
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
    int RegionId,
    string Name,
    string Slug,
    Difficulty Difficulty,
    decimal DistanceKm,
    decimal DurationHours,
    int ElevationGainM = 0,
    string? CreatedBy = null,
    SetRouteDetailDto? Detail = null);

public record UpdateRouteDto(
    string Name,
    Difficulty Difficulty,
    decimal DistanceKm,
    decimal DurationHours,
    int ElevationGainM,
    string? UpdatedBy = null);
