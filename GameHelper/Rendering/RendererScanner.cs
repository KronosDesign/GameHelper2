namespace GameHelper.Rendering;

using System;
using System.Linq;
using System.Reflection;

public static class RendererScanner
{
    public static void ScanAndRegister(RenderLibrary library, Assembly assembly)
    {
        var providerTypes = assembly.GetTypes().Where(t =>
            typeof(IRenderProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var providerType in providerTypes)
        {
            var providerInstance = (IRenderProvider)Activator.CreateInstance(providerType);
            if (providerInstance != null)
            {
                library.Register(providerInstance);
            }
        }
    }
}