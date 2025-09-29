namespace GameHelper.Rendering;

using System;

public abstract class RenderProvider<T> : IRenderProvider
{
    public Type TargetType => typeof(T);
    public RenderLibrary Library { get; set; }

    public bool Render(object obj)
    {
        if (obj is T typed)
        {
            Render(typed);
            return true;
        }

        return false;
    }

    protected void RenderNested(object obj)
    {
        if (Library == null)
        {
            Console.WriteLine("WARNING: RenderLibrary reference not set on provider. Cannot render nested object.");
            return;
        }

        Library.Render(obj);
    }

    protected abstract void Render(T obj);
}