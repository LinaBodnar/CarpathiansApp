using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Catalog.Grpc;
using Carpathians.Domain.Exceptions;
using Grpc.Core;
using DomainDifficulty = Carpathians.Domain.Enums.Difficulty;

namespace Carpathians.Catalog.Api.Grpc;

/// <summary>
/// gRPC-API Каталогу (контракт Protos/catalog.proto) для внутрішніх викликів (Aggregator).
/// Тонкий адаптер над бізнес-логікою Лаб. 03, як і REST-контролер: працює через IRouteService/IRegionService,
/// тобто через декоратори з дворівневим кешем — повторні виклики не звертаються до бази.
/// <para>Помилки: некоректний запит — INVALID_ARGUMENT тут; доменні винятки (EntityNotFoundException -> NOT_FOUND)
/// перетворює <see cref="CatalogGrpcExceptionInterceptor"/>.</para>
/// </summary>
public sealed class CatalogGrpcService(IRouteService routes, IRegionService regions) : CatalogGrpc.CatalogGrpcBase
{
    /// <summary>Максимум ідентифікаторів в одному пакетному запиті.</summary>
    public const int MaxBatchSize = 100;

    public override async Task<RouteReply> GetRoute(GetRouteRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Route id must be positive"));

        var route = await routes.GetByIdAsync(request.Id, context.CancellationToken);
        return ToReply(route);
    }

    public override async Task<RoutesReply> GetRoutesByIds(GetRoutesByIdsRequest request, ServerCallContext context)
    {
        var ids = ValidateIds(request.Ids);
        var reply = new RoutesReply();

        // Послідовно: один DbContext на запит не підтримує паралельних запитів;
        // більшість маршрутів береться з кешу без звернення до бази.
        foreach (var id in ids)
        {
            try
            {
                reply.Routes.Add(ToReply(await routes.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (EntityNotFoundException)
            {
                // відсутні маршрути пропускаються — клієнт бачить, яких id немає у відповіді
            }
        }

        return reply;
    }

    public override async Task<RegionsReply> ListRegions(ListRegionsRequest request, ServerCallContext context)
    {
        var reply = new RegionsReply();
        foreach (var region in await regions.GetAllAsync(context.CancellationToken))
        {
            reply.Regions.Add(new RegionReply
            {
                Id = region.Id,
                Name = region.Name,
                Description = region.Description
            });
        }

        return reply;
    }

    private static List<long> ValidateIds(ICollection<long> ids)
    {
        if (ids.Count == 0 || ids.Count > MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"From 1 to {MaxBatchSize} route ids are expected"));
        if (ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Route ids must be positive"));
        return ids.Distinct().ToList();
    }

    /// <summary>Доменна модель (DTO Лаб. 03) -> повідомлення gRPC.</summary>
    private static RouteReply ToReply(RouteDto route)
    {
        var reply = new RouteReply
        {
            Id = route.Id,
            RegionId = route.RegionId,
            RegionName = route.RegionName,
            Name = route.Name,
            Slug = route.Slug,
            Difficulty = ToProto(route.Difficulty),
            DistanceKm = route.DistanceKm,                  // decimal -> DecimalValue (units + nanos), без втрати точності
            DurationHours = route.DurationHours,
            ElevationGainM = route.ElevationGainM,
            PointsCount = route.RoutePoints.Count
        };

        if (route.Detail is { } detail)
        {
            reply.Detail = new RouteDetail
            {
                Description = detail.Description,
                Season = detail.Season,                     // StringValue: null -> поле відсутнє
                MaxAltitudeM = detail.MaxAltitudeM,
                StartPoint = detail.StartPoint,
                Requirements = detail.Requirements
            };
        }

        return reply;
    }

    private static RouteDifficulty ToProto(DomainDifficulty difficulty) => difficulty switch
    {
        DomainDifficulty.Easy => RouteDifficulty.Easy,
        DomainDifficulty.Moderate => RouteDifficulty.Moderate,
        DomainDifficulty.Hard => RouteDifficulty.Hard,
        _ => RouteDifficulty.Unspecified
    };
}
