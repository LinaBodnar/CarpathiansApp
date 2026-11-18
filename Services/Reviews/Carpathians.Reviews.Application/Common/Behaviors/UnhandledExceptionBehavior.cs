using Carpathians.Reviews.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Carpathians.Reviews.Application.Common.Behaviors;

/// <summary>
/// Централізоване логування винятків на рівні MediatR. Очікувані бізнес-винятки
/// (валідація, не знайдено, конфлікт, доменне правило) пишуться як Warning, решта — як Error
/// разом із запитом, щоб інцидент можна було відтворити. Виняток прокидається далі —
/// у HTTP-статус його перетворює глобальний обробник WebApi.
/// </summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is ValidationException or NotFoundException
                                       or ConflictException or DomainException)
        {
            logger.LogWarning("{RequestName} rejected: {ExceptionType} {Message}",
                typeof(TRequest).Name, ex.GetType().Name, ex.Message);
            ex.MarkLogged();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception for {RequestName} {@Request}",
                typeof(TRequest).Name, request);
            ex.MarkLogged();
            throw;
        }
    }
}
