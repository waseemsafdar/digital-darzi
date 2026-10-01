using Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Validation;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument that has one and
/// short-circuits with 400 and the standard envelope listing each failure. Model-binding errors
/// (malformed JSON, bad dates) are returned in the same shape.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<string>();

        if (!context.ModelState.IsValid)
        {
            errors.AddRange(context.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .SelectMany(kv => kv.Value!.Errors.Select(e =>
                    string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? $"{kv.Key}: invalid value."
                        : string.IsNullOrEmpty(kv.Key) ? e.ErrorMessage : $"{kv.Key}: {e.ErrorMessage}")));
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
                errors.AddRange(result.Errors.Select(e => e.ErrorMessage));
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(ApiResponse<object>.Fail(errors.Distinct().ToList()));
            return;
        }

        await next();
    }
}
