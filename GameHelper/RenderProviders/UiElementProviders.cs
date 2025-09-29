namespace GameHelper.RenderProviders;

using System.Numerics;
using GameHelper.RemoteObjects.UiElement;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class UiElementBaseProvider : RenderProvider<UiElementBase>
{
    protected override void Render(UiElementBase obj)
    {
        // Show explore controls proxy
        var show = GetShow(obj);
        ImGui.Checkbox("Show", ref show);
        SetShow(obj, show);

        ImGui.SameLine();
        if (ImGui.Button("Explore"))
        {
            // Delegate to existing explorer via public API
            GameHelper.Ui.GameUiExplorer.AddUiElement(obj);
        }

        RenderCommon(obj);
    }

    private static void RenderCommon(UiElementBase obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        if (GetShow(obj))
        {
            ImGuiHelper.DrawRect(obj.Postion, obj.Size, 255, 255, 0);
        }

        ImGui.Text($"Position  {obj.Postion}");
        ImGui.Text($"Size  {obj.Size}");
        ImGui.Text($"Unscaled Size {GetUnscaledSize(obj)}");
        ImGui.Text($"IsVisible  {obj.IsVisible}");
        ImGui.Text($"Total Childrens  {obj.TotalChildrens}");
        ImGui.Text($"Parent  {GetParentAddress(obj).ToInt64():X}");
        ImGui.Text($"Position Modifier {GetPositionModifier(obj)}");
        ImGui.Text($"Scale Index {GetScaleIndex(obj)}");
        ImGui.Text($"Local Scale Multiplier {GetLocalScaleMultiplier(obj)}");
        ImGui.Text($"Flags: {GetFlags(obj):X}");
        ImGui.Text("Background Color");
        ImGui.SameLine();
        ImGui.ColorButton("##UiElementBackgroundColor", GetBackgroundColor(obj));

        // Render children lazily
        for (var i = 0; i < obj.TotalChildrens; i++)
        {
            if (ImGui.TreeNode($"Child[{i}]") )
            {
                RenderNested(obj[i]);
                ImGui.TreePop();
            }
        }
    }

    // Reflection helpers for private fields
    private static bool GetShow(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (bool)(f?.GetValue(obj) ?? false);
    }
    private static void SetShow(UiElementBase obj, bool value)
    {
        var f = typeof(UiElementBase).GetField("show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(obj, value);
    }
    private static Vector2 GetUnscaledSize(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("unScaledSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Vector2)(f?.GetValue(obj) ?? Vector2.Zero);
    }
    private static int GetFlags(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("flags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int)(f?.GetValue(obj) ?? 0);
    }
    private static float GetLocalScaleMultiplier(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("localScaleMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (float)(f?.GetValue(obj) ?? 1f);
    }
    private static byte GetScaleIndex(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("scaleIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (byte)(f?.GetValue(obj) ?? (byte)0);
    }
    private static Vector2 GetPositionModifier(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("positionModifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Vector2)(f?.GetValue(obj) ?? Vector2.Zero);
    }
    private static System.IntPtr GetParentAddress(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("parentAddress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (System.IntPtr)(f?.GetValue(obj) ?? System.IntPtr.Zero);
    }
    private static Vector4 GetBackgroundColor(UiElementBase obj)
    {
        var f = typeof(UiElementBase).GetField("backgroundColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Vector4)(f?.GetValue(obj) ?? default(Vector4));
    }
}

public class MapUiElementProvider : RenderProvider<MapUiElement>
{
    protected override void Render(MapUiElement obj)
    {
        RenderNested((UiElementBase)obj);
        ImGui.Text($"Shift {obj.Shift}");
        ImGui.Text($"Default Shift {obj.DefaultShift}");
        ImGui.Text($"Zoom {obj.Zoom}");
    }
}

public class LargeMapUiElementProvider : RenderProvider<LargeMapUiElement>
{
    protected override void Render(LargeMapUiElement obj)
    {
        RenderNested((UiElementBase)obj);
        ImGui.Text($"Center (without shift/default-shift) {obj.Center}");
    }
}

public class SkillTreeNodeUiElementProvider : RenderProvider<SkillTreeNodeUiElement>
{
    protected override void Render(SkillTreeNodeUiElement obj)
    {
        RenderNested((UiElementBase)obj);
        ImGui.Text($"SkillGraphId = {obj.SkillGraphId}");
    }
}

public class ChatParentUiElementProvider : RenderProvider<ChatParentUiElement>
{
    protected override void Render(ChatParentUiElement obj)
    {
        RenderNested((UiElementBase)obj);
        var colorW = typeof(UiElementBase).GetField("backgroundColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var col = (Vector4)(colorW?.GetValue(obj) ?? default(Vector4));
        ImGui.Text($"IsChatActive: {obj.IsChatActive} ({col.W * 255})");
    }
}
