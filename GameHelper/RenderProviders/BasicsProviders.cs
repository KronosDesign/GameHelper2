namespace GameHelper.RenderProviders;

using GameHelper.RemoteObjects;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class AreaChangeCounterProvider : RenderProvider<AreaChangeCounter>
{
    protected override void Render(AreaChangeCounter obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text($"Area Change Counter: {obj.Value}");
    }
}

public class GameWindowScaleProvider : RenderProvider<GameWindowScale>
{
    protected override void Render(GameWindowScale obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text($"Index 1: width, height {obj.GetScaleValue(1, 1)} ratio");
        ImGui.Text($"Index 2: width, height {obj.GetScaleValue(2, 1)} ratio");
        ImGui.Text($"Index 3: width, height {obj.GetScaleValue(3, 1)} ratio");
    }
}

public class GameWindowCullProvider : RenderProvider<GameWindowCull>
{
    protected override void Render(GameWindowCull obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text($"Game Window Cull Size: {obj.Value}");
    }
}

public class TerrainHeightHelperProvider : RenderProvider<TerrainHeightHelper>
{
    protected override void Render(TerrainHeightHelper obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text(string.Join(' ', obj.Values));
    }
}
