using Carpathians.Reviews.Domain.Exceptions;
using MongoDB.Driver;
using FluentValidationException = FluentValidation.ValidationException;

namespace Carpathians.Reviews.WebApi.Middleware;

/// <summary>Відповідність доменних і MongoDB-винятків HTTP-статусам (таблиця 2 завдання).</summary>
public static class ExceptionStatusMapper
{
    public const int ClientClosedRequest = 499;

    public static (int Status, string Title) Map(Exception ex) => ex switch
    {
        FluentValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        DomainException           => (StatusCodes.Status400BadRequest, "Business rule violation"),
        FormatException           => (StatusCodes.Status400BadRequest, "Invalid request format"),
        NotFoundException         => (StatusCodes.Status404NotFound, "Resource not found"),
        ConflictException         => (StatusCodes.Status409Conflict, "Concurrency or state conflict"),

        // порушення унікального індексу (наприклад, ux_reviews_route_author)
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
            => (StatusCodes.Status409Conflict, "Duplicate key"),
        MongoBulkWriteException bulk when bulk.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey)
            => (StatusCodes.Status409Conflict, "Duplicate key"),
        MongoWriteException       => (StatusCodes.Status400BadRequest, "Document write rejected"),

        // база недоступна: обрив з'єднання або таймаут вибору сервера
        MongoConnectionException  => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),
        TimeoutException          => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),

        OperationCanceledException => (ClientClosedRequest, "Client closed request"),
        _                         => (StatusCodes.Status500InternalServerError, "Unexpected error")
    };
}
