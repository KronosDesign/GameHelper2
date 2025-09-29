namespace GameHelper.RenderProviders;

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using GameHelper.RemoteObjects;
using GameHelper.Rendering;
using GameHelper.Utils;
using ImGuiNET;

public class LoadedFilesProvider : RenderProvider<LoadedFiles>
{
    protected override void Render(LoadedFiles obj)
    {
        ImGuiHelper.IntPtrToImGui("Address", obj.Address);
        ImGui.Text($"Total Loaded Files in current area: {obj.PathNames.Count}");

        // filename
        var filename = GetPrivate<string>(obj, "filename") ?? string.Empty;
        ImGui.Text("File Name: ");
        ImGui.SameLine();
        ImGui.InputText("##filename", ref filename, 100);
        SetPrivate(obj, "filename", filename);
        ImGui.SameLine();

        var areaAlreadyDone = GetPrivate<bool>(obj, "areaAlreadyDone");
        if (!areaAlreadyDone)
        {
            if (ImGui.Button("Save"))
            {
                try
                {
                    var dirName = "preload_dumps";
                    Directory.CreateDirectory(dirName);
                    var dataToWrite = obj.PathNames.Keys.ToList();
                    dataToWrite.Sort();
                    File.WriteAllText(Path.Join(dirName, filename), string.Join("\n", dataToWrite));
                    SetPrivate(obj, "areaAlreadyDone", true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save preload dump: {ex.Message}");
                }
            }
        }
        else
        {
            ImGuiHelper.DrawDisabledButton("Save");
        }

        // search box
        var searchText = GetPrivate<string>(obj, "searchText") ?? string.Empty;
        ImGui.Text("Search:    ");
        ImGui.SameLine();
        if (ImGui.InputText("##LoadedFiles", ref searchText, 50))
        {
            var parts = searchText.ToLower().Split(',', StringSplitOptions.RemoveEmptyEntries);
            SetPrivate(obj, "searchTextSplit", parts);
            SetPrivate(obj, "searchText", searchText);
        }

        ImGui.Text("NOTE: Search is Case-Insensitive. Use commas (,) to narrow down the resulting files.");
        if (!string.IsNullOrEmpty(searchText))
        {
            if (ImGui.BeginChild("Result##loadedfiles", Vector2.Zero, ImGuiChildFlags.Borders))
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                var filters = (string[])(GetPrivate<object>(obj, "searchTextSplit") ?? Array.Empty<string>());
                foreach (var kv in obj.PathNames)
                {
                    var containsAll = true;
                    for (var i = 0; i < filters.Length; i++)
                    {
                        if (!kv.Key.ToLower().Contains(filters[i]))
                        {
                            containsAll = false;
                        }
                    }

                    if (containsAll)
                    {
                        if (ImGui.SmallButton($"AreaId: {kv.Value} Path: {kv.Key}"))
                        {
                            ImGui.SetClipboardText(kv.Key);
                        }
                    }
                }

                ImGui.PopStyleColor();
                ImGui.EndChild();
            }
        }
    }

    private static T GetPrivate<T>(object obj, string fieldName)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null) return default;
        var v = f.GetValue(obj);
        if (v is T t) return t;
        return default;
    }

    private static object GetPrivate<object>(object obj, string fieldName)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f?.GetValue(obj);
    }

    private static void SetPrivate(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(obj, value);
    }
}
