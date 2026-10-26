using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Carpathians.Api.Filters;

/// <summary>
/// Action filter that validates action parameters using both DataAnnotations
/// and FluentValidation. Returns ValidationProblemDetails (400) when validation fails.
/// Does NOT use the obsolete FluentValidation.AspNetCore integration.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var serviceProvider = context.HttpContext.RequestServices;
        var allErrors = new Dictionary<string, List<string>>();

        foreach (var (_, value) in context.ActionArguments)
        {
            if (value is null) continue;

            // 1. DataAnnotations validation
            var daResults = new List<ValidationResult>();
            var daContext = new ValidationContext(value);
            if (!Validator.TryValidateObject(value, daContext, daResults, validateAllProperties: true))
            {
                foreach (var vr in daResults)
                {
                    var memberName = vr.MemberNames.FirstOrDefault() ?? string.Empty;
                    if (!allErrors.TryGetValue(memberName, out var list))
                    {
                        list = [];
                        allErrors[memberName] = list;
                    }
                    if (vr.ErrorMessage is not null)
                        list.Add(vr.ErrorMessage);
                }
            }

            // 2. FluentValidation
            var valueType = value.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(valueType);
            var validator = serviceProvider.GetService(validatorType) as IValidator;

            if (validator is not null)
            {
                var fvContext = new ValidationContext<object>(value);
                var fvResult = await validator.ValidateAsync(fvContext, context.HttpContext.RequestAborted);

                if (!fvResult.IsValid)
                {
                    foreach (var failure in fvResult.Errors)
                    {
                        var propName = failure.PropertyName ?? string.Empty;
                        if (!allErrors.TryGetValue(propName, out var list))
                        {
                            list = [];
                            allErrors[propName] = list;
                        }
                        list.Add(failure.ErrorMessage);
                    }
                }
            }
        }

        if (allErrors.Count > 0)
        {
            var problemDetails = new ValidationProblemDetails(
                allErrors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()));
            context.Result = new BadRequestObjectResult(problemDetails);
            return;
        }

        await next();
    }
}
