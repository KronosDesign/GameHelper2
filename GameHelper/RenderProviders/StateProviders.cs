namespace GameHelper.RenderProviders;

using GameHelper.RemoteObjects.States;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class InGameStateProvider : RenderProvider<InGameState>
{
    protected override void Render(InGameState obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGuiHelper.IntPtrToImGui("UiRoot", GetUiRoot(obj));

        if (ImGui.TreeNode("CurrentWorldInstance"))
        {
            RenderNested(obj.CurrentWorldInstance);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode("CurrentAreaInstance"))
        {
            RenderNested(obj.CurrentAreaInstance);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode("GameUi"))
        {
            RenderNested(obj.GameUi);
            ImGui.TreePop();
        }
    }

    private static System.IntPtr GetUiRoot(InGameState obj)
    {
        // Best-effort: reflect the private field if present, otherwise zero
        var fld = typeof(InGameState).GetField("uiRootAddress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (System.IntPtr)(fld?.GetValue(obj) ?? System.IntPtr.Zero);
    }
}

public class AreaLoadingStateProvider : RenderProvider<AreaLoadingState>
{
    protected override void Render(AreaLoadingState obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text($"Current Area Name: {GetAreaName(obj)}");
        ImGui.Text($"Is Loading Screen: {GetIsLoading(obj)}");
        ImGui.Text($"Total Loading Time(ms): {GetTotalLoadingMs(obj)}");
    }

    private static string GetAreaName(AreaLoadingState obj)
    {
        var prop = typeof(AreaLoadingState).GetProperty("CurrentAreaName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return (string)(prop?.GetValue(obj) ?? string.Empty);
    }

    private static bool GetIsLoading(AreaLoadingState obj)
    {
        var prop = typeof(AreaLoadingState).GetProperty("IsLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (bool)(prop?.GetValue(obj) ?? false);
    }

    private static int GetTotalLoadingMs(AreaLoadingState obj)
    {
        var fld = typeof(AreaLoadingState).GetField("lastCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fld?.GetValue(obj) is object cache)
        {
            var totalProp = cache.GetType().GetProperty("TotalLoadingScreenTimeMs");
            if (totalProp != null)
            {
                return (int)(totalProp.GetValue(cache) ?? 0);
            }
        }
        return 0;
    }
}
