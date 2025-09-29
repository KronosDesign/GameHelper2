namespace GameHelper.Rendering;

using System;

public class FallbackProvider : IRenderProvider
{
    public Type TargetType => typeof(object);
    public RenderLibrary Library { get; set; }

    public bool Render(object obj)
    {
        return true;
    }
}