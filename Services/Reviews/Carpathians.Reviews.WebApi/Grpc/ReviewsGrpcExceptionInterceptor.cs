using Carpathians.Platform.ServiceDefaults.Grpc;
using Carpathians.Reviews.Domain.Exceptions;
using Grpc.Core;
using MongoDB.Driver;
using FluentValidationException = FluentValidation.ValidationException;

namespace Carpathians.Reviews.WebApi.Grpc;

/// <summary>
/// Винятки сервісу відгуків -> коди статусу gRPC (аналог ExceptionStatusMapper для REST).
/// Помилки валідації FluentValidation (ValidationBehavior MediatR) — INVALID_ARGUMENT,
/// недоступність MongoDB — UNAVAILABLE (клієнт може повторити виклик).
/// </summary>
public sealed class ReviewsGrpcExceptionInterceptor : GrpcExceptionInterceptor
{
    protected override StatusCode? MapException(Exception exception) => exception switch
    {
        NotFoundException => StatusCode.NotFound,
        FluentValidationException or FormatException => StatusCode.InvalidArgument,
        DomainException => StatusCode.FailedPrecondition,
        ConflictException => StatusCode.Aborted,
        MongoConnectionException or TimeoutException => StatusCode.Unavailable,
        _ => null
    };
}
