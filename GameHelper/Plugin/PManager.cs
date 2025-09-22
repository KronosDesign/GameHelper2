// <copyright file="PManager.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.IO.Compression; // added for zip extraction
    using System.Diagnostics;    // added for dotnet build
    using Coroutine;
    using CoroutineEvents;
    using CTOUtils = ClickableTransparentOverlay.Win32.Utils;
    using Settings;
    using Ui;
    using Utils;

    internal record PluginWithName(string Name, IPCore Plugin);

    internal record PluginContainer(string Name, IPCore Plugin, PluginMetadata Metadata);

    /// <summary>
    ///     Finds, loads and unloads the plugins.
    /// </summary>
    internal static class PManager
    {
        private static bool disableRendering = false;
#if DEBUG
        internal static readonly List<string> PluginNames = new();
#endif
        internal static readonly List<PluginContainer> Plugins = new();

        /// <summary>
        ///     Initlizes the plugin manager by loading all the plugins and their Metadata.
        /// </summary>
        internal static void InitializePlugins()
        {
            State.PluginsDirectory.Create(); // doesn't do anything if already exists.

            // Expand any .zip files dropped in the Plugins folder before scanning directories.
            ExtractZipPluginsIfAny();

            // Respect the new setting: rebuild everything, otherwise only build if missing DLL.
            if (Core.GHSettings.AlwaysRebuildPlugins)
            {
                Console.WriteLine($"[PManager] AlwaysRebuildProject is enabled; rebuilding all plugin projects.");
                ForceRebuildAllProjects();
            }
            else
            {
                EnsureBuiltPluginsInPlace();
            }

            LoadPluginMetadata(LoadPlugins());
#if DEBUG
            GetAllPluginNames();
#endif
            Parallel.ForEach(Plugins, EnablePluginIfRequired);
            CoroutineHandler.Start(SavePluginSettingsCoroutine());
            CoroutineHandler.Start(SavePluginMetadataCoroutine());
            Core.CoroutinesRegistrar.Add(CoroutineHandler.Start(
                DrawPluginUiRenderCoroutine(), "[PManager] Draw Plugins UI"));
        }

        /// <summary>
        /// Extract any non-hidden *.zip files in the Plugins directory.
        /// Destination folder name is taken from the plugin DLL file name (without extension).
        /// - If the ZIP already contains a single top-level folder, avoid nesting by moving that folder.
        /// - Prefer/builder produces a DLL; ensure a DLL exists at root of final folder.
        /// - Delete the ZIP after a successful extraction.
        /// </summary>
        private static void ExtractZipPluginsIfAny()
        {
            try
            {
                var zips = State.PluginsDirectory
                    .GetFiles("*.zip", SearchOption.TopDirectoryOnly)
                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                    .ToList();

                foreach (var zip in zips)
                {
                    string tempDir = null;
                    try
                    {
                        tempDir = Path.Combine(State.PluginsDirectory.FullName, Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tempDir);

                        // Extract into a temporary folder
                        ZipFile.ExtractToDirectory(zip.FullName, tempDir, overwriteFiles: true);

                        // Detect if archive has a single root directory (no files at temp root, exactly one directory)
                        var topFiles = Directory.GetFiles(tempDir, "*", SearchOption.TopDirectoryOnly);
                        var topDirs = Directory.GetDirectories(tempDir, "*", SearchOption.TopDirectoryOnly);

                        // If there is exactly one top-level folder and no top-level files, use that as content root
                        var contentRoot = (topFiles.Length == 0 && topDirs.Length == 1) ? topDirs[0] : tempDir;

                        // Try to build if project/solution exists; else pick an existing DLL similar to zip base name
                        string targetName = Path.GetFileNameWithoutExtension(zip.Name);
                        string builtDllPath = TryBuildIfProject(contentRoot, targetName);
                        string dllPath = builtDllPath ?? PickDllBySimilarity(contentRoot, targetName);

                        if (dllPath == null)
                        {
                            Console.WriteLine($"[PManager] No dll found or produced in '{zip.Name}', skipping.");
                            Directory.Delete(tempDir, true);
                            continue;
                        }

                        // Ensure the DLL is present at the root of the contentRoot
                        var dllAtRoot = EnsureDllAtRoot(contentRoot, dllPath);

                        // Final folder name comes from the dll's base name
                        var dllBaseName = Path.GetFileNameWithoutExtension(dllAtRoot);
                        var finalDir = Path.Combine(State.PluginsDirectory.FullName, dllBaseName);

                        // If the destination already exists, remove it to avoid nesting/merge issues
                        if (Directory.Exists(finalDir))
                        {
                            try
                            {
                                Directory.Delete(finalDir, true);
                            }
                            catch (Exception delEx)
                            {
                                Console.WriteLine($"[PManager] Failed clearing existing folder '{finalDir}': {delEx}");
                                // If we can't clear, skip to next zip to avoid undefined state
                                Directory.Delete(tempDir, true);
                                continue;
                            }
                        }

                        // Move the content root into place:
                        if (string.Equals(contentRoot, tempDir, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Move(tempDir, finalDir);
                        }
                        else
                        {
                            Directory.Move(contentRoot, finalDir);
                            // Clean up the now-empty tempDir that still exists alongside
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }

                        // Ensure we have a DLL named exactly as the folder (e.g., {Folder}.dll)
                        EnsureDllNamedAfterFolder(finalDir);

                        // Remove the zip after successful extraction/placement
                        zip.Delete();

                        Console.WriteLine($"[PManager] Extracted '{zip.Name}' to '{finalDir}' and removed the archive.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PManager] Failed to extract '{zip.FullName}': {ex}");
                        // Best-effort cleanup
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(tempDir) && Directory.Exists(tempDir))
                                Directory.Delete(tempDir, true);
                        }
                        catch
                        {
                            // ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] ZIP scan failed: {ex}");
            }
        }

        /// <summary>
        /// NEW: For each plugin directory, if there is no top-level DLL matching the folder name,
        /// but a .csproj or .sln exists in the tree, compile and place the resulting DLL at the root.
        /// Also ensure a DLL exists named exactly {Folder}.dll (copy/rename as needed).
        /// </summary>
        private static void EnsureBuiltPluginsInPlace()
        {
            try
            {
                var dirs = GetPluginsDirectories();
                foreach (var dir in dirs)
                {
                    try
                    {
                        // Do we already have a DLL at top level that matches the loader pattern?
                        var topDll = dir.GetFiles($"{dir.Name}*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (topDll != null)
                        {
                            // Optionally enforce exact name {Folder}.dll for consistency
                            EnsureDllNamedAfterFolder(dir.FullName);
                            continue;
                        }

                        // If no matching dll, but has a project/solution, try to build.
                        var hasProjOrSln =
                            Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.AllDirectories).Any() ||
                            Directory.EnumerateFiles(dir.FullName, "*.sln", SearchOption.AllDirectories).Any();

                        if (!hasProjOrSln)
                        {
                            continue;
                        }

                        Console.WriteLine($"[PManager] Building plugin folder '{dir.Name}' due to missing top-level dll.");
                        var builtDll = TryBuildIfProject(dir.FullName, dir.Name);
                        if (builtDll == null)
                        {
                            Console.WriteLine($"[PManager] Build did not produce a DLL for '{dir.Name}'.");
                            continue;
                        }

                        // Ensure the built DLL is at root
                        var dllAtRoot = EnsureDllAtRoot(dir.FullName, builtDll);

                        // Ensure we have a DLL named exactly as the folder
                        EnsureDllNamedAfterFolder(dir.FullName);

                        Console.WriteLine($"[PManager] Built and prepared plugin '{dir.Name}'.");
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"[PManager] EnsureBuiltPluginsInPlace error for '{dir.FullName}': {exInner}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] EnsureBuiltPluginsInPlace failed: {ex}");
            }
        }

        /// <summary>
        /// If a .csproj or .sln exists in contentRoot, attempts to build it with `dotnet build -c Release`.
        /// On success, returns the path to the produced DLL (favoring names similar to targetName, else newest).
        /// Returns null on failure or if no project/solution exists.
        /// </summary>
        private static string TryBuildIfProject(string contentRoot, string targetName)
        {
            try
            {
                var csprojs = Directory.GetFiles(contentRoot, "*.csproj", SearchOption.AllDirectories);
                var slns = Directory.GetFiles(contentRoot, "*.sln", SearchOption.AllDirectories);

                if (csprojs.Length == 0 && slns.Length == 0)
                {
                    return null;
                }

                // Prefer building a csproj that best matches the targetName; otherwise first one.
                string projToBuild = csprojs
                    .OrderBy(p => SimilarityScore(Path.GetFileNameWithoutExtension(p), targetName))
                    .FirstOrDefault();

                bool isSolution = false;
                if (projToBuild == null && slns.Length > 0)
                {
                    // If only a solution is present (or no good csproj match), build the solution.
                    projToBuild = slns
                        .OrderBy(s => SimilarityScore(Path.GetFileNameWithoutExtension(s), targetName))
                        .First();
                    isSolution = true;
                }

                if (projToBuild == null)
                {
                    return null;
                }

                var workDir = Path.GetDirectoryName(projToBuild) ?? contentRoot;
                var args = $"build \"{projToBuild}\" -c Release -nologo";

                Console.WriteLine($"[PManager] Building {(isSolution ? "solution" : "project")} '{projToBuild}'...");
                var (exitCode, stdout, stderr) = RunProcess("dotnet", args, workDir);

                if (exitCode != 0)
                {
                    Console.WriteLine($"[PManager] Build failed (exit {exitCode}).\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
                    return null;
                }

                // Search for DLLs under bin/Release of the project (or of all projects if solution)
                var searchRoots = isSolution
                    ? Directory.GetDirectories(contentRoot, "*", SearchOption.AllDirectories)
                    : new[] { Path.GetDirectoryName(projToBuild) ?? contentRoot };

                var releaseDlls = searchRoots
                    .SelectMany(r =>
                    {
                        var bins = Directory.GetDirectories(r, "bin", SearchOption.TopDirectoryOnly);
                        return bins.SelectMany(b =>
                        {
                            var rel = Path.Combine(b, "Release");
                            return Directory.Exists(rel)
                                ? Directory.EnumerateFiles(rel, "*.dll", SearchOption.AllDirectories)
                                : Array.Empty<string>();
                        });
                    })
                    .Distinct()
                    .ToList();

                if (releaseDlls.Count == 0)
                {
                    Console.WriteLine($"[PManager] Build succeeded but produced no DLLs discoverable under bin/Release.");
                    return null;
                }

                // Prefer DLL whose name matches targetName, else newest by write time
                var picked = releaseDlls
                    .OrderBy(p => SimilarityScore(Path.GetFileNameWithoutExtension(p), targetName))
                    .ThenByDescending(p => GetSafeWriteTimeUtc(p))
                    .First();

                Console.WriteLine($"[PManager] Build produced '{picked}'.");
                return picked;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] TryBuildIfProject error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// When AlwaysRebuildProject = true:
        /// For each plugin folder:
        ///   - If any .sln exists, build solutions only (do not build projects).
        ///   - Else if no .sln but .csproj exists, build projects only.
        ///   - After building, ensure a DLL is at the folder root and that {Folder}.dll exists.
        /// </summary>
        private static void ForceRebuildAllProjects()
        {
            try
            {
                var dirs = GetPluginsDirectories();
                foreach (var dir in dirs)
                {
                    try
                    {
                        var slns = Directory.GetFiles(dir.FullName, "*.sln", SearchOption.AllDirectories);
                        var csprojs = Directory.GetFiles(dir.FullName, "*.csproj", SearchOption.AllDirectories);

                        if (slns.Length > 0)
                        {
                            // Build solutions only for this directory
                            foreach (var sln in slns)
                            {
                                //Console.WriteLine($"[PManager] Rebuilding solution '{sln}' (AlwaysRebuildProject)...");
                                var _ = TryBuildIfProject(dir.FullName, Path.GetFileNameWithoutExtension(sln));
                            }
                        }
                        else if (csprojs.Length > 0)
                        {
                            // No solutions; build projects only for this directory
                            foreach (var proj in csprojs)
                            {
                                //Console.WriteLine($"[PManager] Rebuilding project '{proj}' (AlwaysRebuildProject)...");
                                var _ = TryBuildIfProject(dir.FullName, Path.GetFileNameWithoutExtension(proj));
                            }
                        }
                        else
                        {
                            // Nothing to build in this directory
                        }

                        // Prefer a DLL matching the folder name; else pick newest found in tree
                        var preferred = PickDllBySimilarity(dir.FullName, dir.Name);
                        if (preferred != null)
                        {
                            // Ensure it’s at the folder root
                            var dllAtRoot = EnsureDllAtRoot(dir.FullName, preferred);
                        }

                        // Ensure a DLL named exactly {Folder}.dll exists at the root
                        EnsureDllNamedAfterFolder(dir.FullName);

                        //Console.WriteLine($"[PManager] Force rebuilt and prepared plugin '{dir.Name}'.");
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"[PManager] ForceRebuildAllProjects error for '{dir.FullName}': {exInner}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] ForceRebuildAllProjects failed: {ex}");
            }
        }

        /// <summary>
        /// Ensure the specified dll is present at the root of contentRoot.
        /// If nested, copies it up to contentRoot and returns that path.
        /// </summary>
        private static string EnsureDllAtRoot(string contentRoot, string dllPath)
        {
            try
            {
                var rel = Path.GetRelativePath(contentRoot, dllPath);
                var relDir = Path.GetDirectoryName(rel);
                if (string.IsNullOrEmpty(relDir) || relDir == "." || relDir == Path.DirectorySeparatorChar.ToString())
                {
                    return dllPath; // already at root
                }

                var rootCopy = Path.Combine(contentRoot, Path.GetFileName(dllPath));
                File.Copy(dllPath, rootCopy, overwrite: true);
                return rootCopy;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] Failed to ensure DLL at root: {ex}");
                return dllPath;
            }
        }

        /// <summary>
        /// Ensure there is a DLL named exactly {FolderName}.dll at the folder's top level
        /// (copy/rename from any top-level dll if necessary).
        /// </summary>
        private static void EnsureDllNamedAfterFolder(string folder)
        {
            try
            {
                var folderName = new DirectoryInfo(folder).Name;
                var expected = Path.Combine(folder, folderName + ".dll");

                if (File.Exists(expected))
                    return;

                // If a top-level dll exists, copy/rename it to {Folder}.dll
                var anyTopDll = Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly)
                                         .FirstOrDefault();
                if (anyTopDll != null)
                {
                    File.Copy(anyTopDll, expected, overwrite: true);
                    return;
                }

                // Otherwise, try to find any dll anywhere and copy it to the top as {Folder}.dll
                var anyDll = Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories)
                                      .FirstOrDefault();
                if (anyDll != null)
                {
                    File.Copy(anyDll, expected, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PManager] EnsureDllNamedAfterFolder error for '{folder}': {ex}");
            }
        }

        /// <summary>
        /// Pick a DLL inside contentRoot, favoring name similarity to a target (e.g., zip base name).
        /// If none match, pick the newest by write time.
        /// </summary>
        private static string PickDllBySimilarity(string contentRoot, string targetBaseName)
        {
            var dlls = Directory.EnumerateFiles(contentRoot, "*.dll", SearchOption.AllDirectories).ToList();
            if (dlls.Count == 0)
            {
                return null;
            }

            return dlls
                .OrderBy(p => SimilarityScore(Path.GetFileNameWithoutExtension(p), targetBaseName))
                .ThenByDescending(p => GetSafeWriteTimeUtc(p))
                .First();
        }

        /// <summary>
        /// Lower is better. 0 = exact (case-insensitive), 1 = startswith, 2 = contains, 3 = other.
        /// </summary>
        private static int SimilarityScore(string candidate, string target)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
                return 3;

            var c = candidate.Trim();
            var t = target.Trim();
            if (c.Equals(t, StringComparison.OrdinalIgnoreCase)) return 0;
            if (c.StartsWith(t, StringComparison.OrdinalIgnoreCase)) return 1;
            if (c.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            return 3;
        }

        private static DateTime GetSafeWriteTimeUtc(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
        }

        private static (int exitCode, string stdout, string stderr) RunProcess(string fileName, string arguments, string workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                return (proc.ExitCode, stdout, stderr);
            }
            catch (Exception ex)
            {
                return (-1, "", $"Process start failed: {ex}");
            }
        }

        private static List<PluginWithName> LoadPlugins()
        {
            return GetPluginsDirectories()
                  .AsParallel()
                  .Select(LoadPlugin)
                  .Where(x => x != null)
                  .OrderBy(x => x.Name)
                  .ToList();
        }

#if DEBUG
        private static void GetAllPluginNames()
        {
            foreach (var plugin in Plugins)
            {
                PluginNames.Add(plugin.Name);
            }
        }

        /// <summary>
        ///     Cleans up the already loaded plugins.
        /// </summary>
        internal static bool UnloadPlugin(string name)
        {
            foreach (var plugin in Plugins)
            {
                if (plugin.Name == name)
                {
                    plugin.Plugin.SaveSettings();
                    plugin.Plugin.OnDisable();
                    Plugins.Remove(plugin);
                    return true;
                }
            }

            return false;
        }

        internal static bool LoadPlugin(string name)
        {
            try
            {
                var container = GetPluginsDirectories()
                                .Where(x => x.Name.Contains(name))
                                .Select(LoadPlugin)
                                .Where(y => y != null)
                                .ToList();
                if (container.Count > 0)
                {
                    LoadPluginMetadata(container);
                    container[0].Plugin.OnEnable(Core.Process.Address != IntPtr.Zero);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
#endif

        private static List<DirectoryInfo> GetPluginsDirectories()
        {
            return State.PluginsDirectory.GetDirectories().Where(
                x => (x.Attributes & FileAttributes.Hidden) == 0).ToList();
        }

        private static Assembly ReadPluginFiles(DirectoryInfo pluginDirectory)
        {
            try
            {
                var dllFile = pluginDirectory.GetFiles(
                    $"{pluginDirectory.Name}*.dll",
                    SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (dllFile == null)
                {
                    Console.WriteLine($"Couldn't find plugin dll with name {pluginDirectory.Name}" +
                                      $" in directory {pluginDirectory.FullName}." +
                                      " Please make sure DLL & the plugin got same name.");
                    return null;
                }

                return new PluginAssemblyLoadContext(dllFile.FullName)
                   .LoadFromAssemblyPath(dllFile.FullName);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load plugin {pluginDirectory.FullName} due to {e}");
                return null;
            }
        }

        private static PluginWithName LoadPlugin(DirectoryInfo pluginDirectory)
        {
            var assembly = ReadPluginFiles(pluginDirectory);
            if (assembly != null)
            {
                var relativePluginDir = pluginDirectory.FullName.Replace(
                    State.PluginsDirectory.FullName, State.PluginsDirectory.Name);
                return LoadPlugin(assembly, relativePluginDir);
            }

            return null;
        }

        private static PluginWithName LoadPlugin(Assembly assembly, string pluginRootDirectory)
        {
            try
            {
                var types = assembly.GetTypes();
                if (types.Length <= 0)
                {
                    Console.WriteLine($"Plugin (in {pluginRootDirectory}) {assembly} doesn't " +
                                      "contain any types (i.e. classes/stuctures).");
                    return null;
                }

                var iPluginClasses = types.Where(
                    type => typeof(IPCore).IsAssignableFrom(type) &&
                            type.IsSealed).ToList();
                if (iPluginClasses.Count != 1)
                {
                    Console.WriteLine($"Plugin (in {pluginRootDirectory}) {assembly} contains" +
                                      $" {iPluginClasses.Count} sealed classes derived from CoreBase<TSettings>." +
                                      " It should have one sealed class derived from IPlugin.");
                    return null;
                }

                var pluginCore = Activator.CreateInstance(iPluginClasses[0]) as IPCore;
                pluginCore.SetPluginDllLocation(pluginRootDirectory);
                return new PluginWithName(assembly.GetName().Name, pluginCore);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading plugin {assembly.FullName} due to {e}");
                return null;
            }
        }

        private static void LoadPluginMetadata(IEnumerable<PluginWithName> plugins)
        {
            var metadata = JsonHelper.CreateOrLoadJsonFile<Dictionary<string, PluginMetadata>>(State.PluginsMetadataFile);
            Plugins.AddRange(
                plugins.Select(
                    x => new PluginContainer(
                        x.Name,
                        x.Plugin,
                        metadata.GetValueOrDefault(
                            x.Name,
                            new PluginMetadata()))));

            SavePluginMetadata();
        }

        private static void EnablePluginIfRequired(PluginContainer container)
        {
            if (container.Metadata.Enable)
            {
                container.Plugin.OnEnable(Core.Process.Address != IntPtr.Zero);
            }
        }

        private static void SavePluginMetadata()
        {
            JsonHelper.SafeToFile(Plugins.ToDictionary(x => x.Name, x => x.Metadata), State.PluginsMetadataFile);
        }

        private static IEnumerator<Wait> SavePluginMetadataCoroutine()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.TimeToSaveAllSettings);
                SavePluginMetadata();
            }
        }

        private static IEnumerator<Wait> SavePluginSettingsCoroutine()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.TimeToSaveAllSettings);
                foreach (var container in Plugins)
                {
                    container.Plugin.SaveSettings();
                }
            }
        }

        private static IEnumerator<Wait> DrawPluginUiRenderCoroutine()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnRender);
                if (CTOUtils.IsKeyPressedAndNotTimeout(Core.GHSettings.DisableAllRenderingKey))
                {
                    disableRendering = !disableRendering;
                }

                if (disableRendering)
                {
                    continue;
                }

                foreach (var container in Plugins)
                {
                    if (container.Metadata.Enable)
                    {
                        using var _ = PerformanceProfiler.Profile(container.Plugin.GetType().FullName, "DrawUI");
                        container.Plugin.DrawUI();
                    }
                }
            }
        }
    }
}
