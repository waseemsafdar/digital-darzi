using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.Json;

/// <summary>
/// Binds <see cref="DateTime"/> and <see cref="Nullable{DateTime}"/> from the query string or route
/// with the same rules as <see cref="UtcDateTimeConverter"/>: offsets are converted to UTC, and a
/// value with no zone ("2026-09-01") is taken as UTC. Without this, <c>?from=2026-09-01</c> binds
/// as Kind=Unspecified and Npgsql rejects it for a <c>timestamp with time zone</c> parameter.
/// </summary>
public sealed class UtcDateTimeModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var raw = value.FirstValue;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) == null)
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "A date is required.");
            return Task.CompletedTask;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"'{raw}' is not a valid date.");
        }
        return Task.CompletedTask;
    }
}

public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = context.Metadata.UnderlyingOrModelType;
        // Only simple values (query/route/form); bodies go through the JSON converters.
        return type == typeof(DateTime) && context.BindingInfo.BindingSource != Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body
            ? new UtcDateTimeModelBinder()
            : null;
    }
}
