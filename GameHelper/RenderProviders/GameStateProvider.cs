namespace GameHelper.RenderProviders;

using GameHelper.RemoteObjects;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class GameStateProvider : RenderProvider<GameStates>
{
    protected override void Render(GameStates obj)
    {
        // Common info
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);

        // GameStates-specific info
        if (ImGui.TreeNode("All States Info"))
        {
            foreach (var state in obj.AllStates)
            {
                ImGuiHelper.IntPtrToImGui($"{state.Value}", state.Key);
            }

            ImGui.TreePop();
        }

        ImGui.Text($"Current State: {obj.GameCurrentState}");
    }
}