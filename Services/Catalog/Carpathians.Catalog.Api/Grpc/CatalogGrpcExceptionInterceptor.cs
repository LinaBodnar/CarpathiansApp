using Carpathians.Domain.Exceptions;
using Carpathians.Platform.ServiceDefaults.Grpc;
using Grpc.Core;

namespace Carpathians.Catalog.Api.Grpc;

/// <summary>Доменні винятки Каталогу -> коди статусу gRPC (аналог DomainExceptionHandler для REST).</summary>
public sealed class CatalogGrpcExceptionInterceptor : GrpcExceptionInterceptor
{
    protected override StatusCode? MapException(Exception exception) => exception switch
    {
        EntityNotFoundException => StatusCode.NotFound,
        ValidationException => StatusCode.InvalidArgument,
        ConflictException => StatusCode.AlreadyExists,
        DomainException => StatusCode.FailedPrecondition,
        _ => null
    };
}
