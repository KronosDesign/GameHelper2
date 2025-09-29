namespace GameHelper.Rendering;

using System;

public interface IRenderProvider
{
    Type TargetType { get; }
    RenderLibrary Library { get; set; }
    bool Render(object obj);
}