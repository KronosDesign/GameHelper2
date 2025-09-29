namespace GameHelper.RenderProviders;

using GameHelper.RemoteObjects.Components;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class ComponentBaseProvider : RenderProvider<ComponentBase>
{
    protected override void Render(ComponentBase obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        var ownerField = typeof(ComponentBase).GetField("OwnerEntityAddress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var owner = (System.IntPtr)(ownerField?.GetValue(obj) ?? System.IntPtr.Zero);
        ImGuiHelper.IntPtrToImGui("Owner Address", owner);
    }
}

public class RenderComponentProvider : RenderProvider<Render>
{
    protected override void Render(Render obj)
    {
        RenderNested((ComponentBase)obj);
        ImGui.Text($"Grid Position: {obj.GridPosition}");
        ImGui.Text($"World Position: {obj.WorldPosition}");
        ImGui.Text($"Terrain Height (Z-Axis): {obj.TerrainHeight}");
        ImGui.Text($"Model Bounds: {obj.ModelBounds}");
    }
}
