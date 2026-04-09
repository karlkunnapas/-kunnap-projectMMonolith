using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebApp.Helpers;

public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var rawValue = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (bindingContext.ModelType == typeof(decimal?))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (TryParseDecimal(rawValue, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"The value '{rawValue}' is not valid for {bindingContext.ModelMetadata.DisplayName ?? bindingContext.ModelName}.");

        return Task.CompletedTask;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        // First, try current UI culture parsing.
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
        {
            return true;
        }

        // Fallback for locale-mismatched decimal separators coming from browser/JS.
        var normalized = value.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
        {
            return new FlexibleDecimalModelBinder();
        }

        return null;
    }
}

