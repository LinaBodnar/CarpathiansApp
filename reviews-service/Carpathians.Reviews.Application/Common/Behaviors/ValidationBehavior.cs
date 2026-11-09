using FluentValidation;
using MediatR;

namespace Carpathians.Reviews.Application.Common.Behaviors;

/// <summary>
/// Виконує всі валідатори FluentValidation для команди чи запиту до виклику обробника.
/// Помилки всіх валідаторів збираються разом; за їх наявності кидається ValidationException,
/// і обробник не викликається.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);            // обробник не викличеться

        return await next();
    }
}
