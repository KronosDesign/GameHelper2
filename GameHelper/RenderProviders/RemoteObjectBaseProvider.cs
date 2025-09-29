namespace GameHelper.RenderProviders;

using System.Reflection;
using GameHelper.RemoteObjects;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class RemoteObjectBaseProvider : RenderProvider<RemoteObjectBase>
{
    protected override void Render(RemoteObjectBase obj)
    {
        // Show address
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);

        // Reflect and render nested RemoteObjectBase properties using the library
        var propFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        foreach (var property in RemoteObjectBase.GetToImGuiMethods(obj.GetType(), propFlags, obj))
        {
            if (ImGui.TreeNode(property.Name))
            {
                RenderNested(property.Value);
                ImGui.TreePop();
            }
        }
    }
}
