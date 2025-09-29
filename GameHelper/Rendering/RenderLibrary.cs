namespace GameHelper.Rendering;

using System;
using System.Collections.Generic;

public class RenderLibrary
{
    private readonly Dictionary<Type, IRenderProvider> providers = new();

    private readonly Dictionary<Type, IRenderProvider> renderCache = new();

    private IRenderProvider fallback = new FallbackProvider();

    public void Register(IRenderProvider provider)
    {
        if (provider?.TargetType != null)
        {
            provider.Library = this;
            providers[provider.TargetType] = provider;
        }
    }

    public void Render(object obj)
    {
        if (obj == null) return;
        var objectType = obj.GetType();
        if (!renderCache.TryGetValue(objectType, out var providerToUse))
        {
            var currentType = objectType;
            while (currentType != null)
            {
                if (providers.TryGetValue(currentType, out var foundProvider))
                {
                    providerToUse = foundProvider;
                    break;
                }

                currentType = currentType.BaseType;
            }

            renderCache[objectType] = providerToUse;
        }

        if (providerToUse != null)
        {
            providerToUse.Render(obj);
        }
        else
        {
            fallback.Render(obj);
        }
    }
}