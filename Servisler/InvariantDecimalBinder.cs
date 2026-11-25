using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DernekTakip.Servisler;

public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var ham = ctx.ValueProvider.GetValue(ctx.ModelName).FirstValue;
        if (string.IsNullOrWhiteSpace(ham))
        {
            if (ctx.ModelMetadata.ModelType == typeof(decimal?))
                ctx.Result = ModelBindingResult.Success(null);
            else
                ctx.Result = ModelBindingResult.Success(0m);
            return Task.CompletedTask;
        }
        if (Depo.ParaOku(ham, out var n))
            ctx.Result = ModelBindingResult.Success(n);
        else
            ctx.ModelState.TryAddModelError(ctx.ModelName, "Tutar geçersiz.");
        return Task.CompletedTask;
    }
}

public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var t = context.Metadata.ModelType;
        if (t == typeof(decimal) || t == typeof(decimal?))
            return new InvariantDecimalModelBinder();
        return null;
    }
}
