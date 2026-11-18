using Carpathians.Domain.Exceptions;
using Carpathians.Platform.ServiceDefaults.Grpc;
using Grpc.Core;

namespace Carpathians.Api.Grpc;

/// <summary>Доменні винятки сервісу бронювань -> коди статусу gRPC (аналог DomainExceptionHandler для REST).</summary>
public sealed class BookingGrpcExceptionInterceptor : GrpcExceptionInterceptor
{
    protected override StatusCode? MapException(Exception exception) => exception switch
    {
        NotFoundException => StatusCode.NotFound,
        ValidationException => StatusCode.InvalidArgument,
        BusinessConflictException => StatusCode.FailedPrecondition,
        _ => null
    };
}
