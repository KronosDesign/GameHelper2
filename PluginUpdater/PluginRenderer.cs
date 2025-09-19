// <copyright file="PluginRenderer.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace PluginUpdater
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using ImGuiNET;
    using GameHelper;
    using Coroutine;
    using GameHelper.CoroutineEvents;

    /// <summary>
    /// Renders the plugin UI using ImGui, based on exCore2 implementation.
    /// </summary>
    public class PluginRenderer : IDisposable
    {
        private readonly PluginUpdaterSettings _settings;
        private GitUpdater _updater;

        private bool _isUpdating;
        private readonly Dictionary<string, bool> _updatingPlugins = new();
        private int _currentProgress;
        private int _totalProgress;
        private bool _isUpdatingAll;
        private string _repoUrl = string.Empty;
        private bool _isCloning;
        private string _pluginNameFilter = "";
        private string _pluginToDelete = "";
        private bool _showDeleteConfirmation;

        // Store functionality
        private readonly List<StorePluginInfo> _storePlugins = new();
        private string _storeSearchFilter = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginRenderer"/> class.
        /// </summary>
        /// <param name="settings">Plugin settings.</param>
        public PluginRenderer(PluginUpdaterSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Initializes the plugin renderer.
        /// </summary>
        public void Startup()
        {
            try
            {
                // Get GameHelper executable path - need to go up from Plugins/PluginUpdater/ to net8.0/
                var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var gameHelperPath = Path.Combine(currentPath ?? "", "..", "..");
                gameHelperPath = Path.GetFullPath(gameHelperPath);
                
                // Create directories at the same level as GameHelper executable (inside net8.0/)
                var sourceDir = Path.Combine(gameHelperPath, "PluginSources");
                var pluginDir = Path.Combine(gameHelperPath, "Plugins");
                
                Console.WriteLine($"GameHelper path: {gameHelperPath}");
                Console.WriteLine($"Creating GitUpdater with sourceDir: {sourceDir}");
                Console.WriteLine($"Creating GitUpdater with pluginDir: {pluginDir}");
                _updater = new GitUpdater(sourceDir, pluginDir);
                
                // Initialize store plugins
                InitializeStore();
                
                _updater.ProgressChanged += (current, total) =>
                {
                    _currentProgress = current;
                    _totalProgress = total;
                };

                var manuallyDownloadedPlugins = _updater.GetManualPlugins();
                foreach (var plugin in manuallyDownloadedPlugins)
                {
                    Console.WriteLine($"{plugin.Name} was downloaded manually so cannot be updated via this plugin");
                }

                if (_settings.CheckUpdatesOnStartup && !_settings.HasCheckedUpdates)
                {
                    _settings.HasCheckedUpdates = true;
                    _currentProgress = 0;
                    _totalProgress = 0;
                    _ = UpdateGitInfoAsync();
                }
            }
            catch (Exception ex)
            {
                _updater = null;
                Console.WriteLine($"Failed to initialize GitUpdater: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the plugin renderer periodically.
        /// </summary>
        public void Update()
        {
            if (_settings.ShouldPerformPeriodicCheck())
            {
                _settings.LastUpdateCheck = DateTime.Now;
                _ = UpdateGitInfoAsync();
            }
        }

        /// <summary>
        /// Renders the main plugin UI with tabs.
        /// </summary>
        public void Render()
        {
            // Use toda a área disponível da janela
            var contentSize = ImGui.GetContentRegionAvail();
            
            if (ImGui.BeginTabBar("PluginManagerTabs", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("Manage"))
                {
                    // Usar toda a altura disponível menos o espaço para botões
                    var availableSize = ImGui.GetContentRegionAvail();
                    
                    RenderUpdateButtons();
                    ImGui.Spacing();
                    
                    // Calcular altura restante para a tabela
                    var remainingHeight = ImGui.GetContentRegionAvail().Y - 10; // pequena margem
                    RenderPluginsTable(remainingHeight);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Add"))
                {
                    ImGui.Spacing();
                    RenderAddPluginSection();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Store"))
                {
                    ImGui.Spacing();
                    RenderStoreSection();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Settings"))
                {
                    ImGui.Spacing();
                    RenderSettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            
            // Modal de confirmação de delete
            RenderDeleteConfirmationModal();
        }

        private void RenderSettings()
        {
            bool checkStartup = _settings.CheckUpdatesOnStartup;
            if (ImGui.Checkbox("Check for updates on startup", ref checkStartup))
            {
                _settings.CheckUpdatesOnStartup = checkStartup;
                _settings.HasCheckedUpdates = false;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Automatically check for plugin updates when the game starts");
            }

            ImGui.Spacing();

            bool autoCheck = _settings.AutoCheckUpdates;
            if (ImGui.Checkbox("Automatically check for updates periodically", ref autoCheck))
            {
                _settings.AutoCheckUpdates = autoCheck;
            }

            if (autoCheck)
            {
                ImGui.Spacing();
                int intervalMinutes = _settings.UpdateCheckIntervalMinutes;
                if (ImGui.SliderInt("Update interval (minutes)", ref intervalMinutes, 5, 120))
                {
                    _settings.UpdateCheckIntervalMinutes = intervalMinutes;
                }
            }

            ImGui.Spacing();

            bool wrapLogMessages = _settings.WrapLogMessages;
            if (ImGui.Checkbox("Wrap log messages", ref wrapLogMessages))
            {
                _settings.WrapLogMessages = wrapLogMessages;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Application Controls:");
            ImGui.Spacing();

            ImGui.SameLine();

            if (ImGui.Button("Restart GameHelper", new System.Numerics.Vector2(200, 30)))
            {
                RestartApplication();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Restart the entire application (use only if reload doesn't work)");
            }
        }

        private void RenderDeleteConfirmationModal()
        {
            if (_showDeleteConfirmation)
            {
                // Centralizar o modal na tela
                var center = ImGui.GetMainViewport().GetCenter();
                ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f, 0.5f));
                
                ImGui.OpenPopup("Confirm Delete");
            }

            if (ImGui.BeginPopupModal("Confirm Delete", ref _showDeleteConfirmation, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped($"Are you sure you want to delete the plugin '{_pluginToDelete}'?");
                ImGui.TextWrapped("This action cannot be undone and will permanently remove all plugin files.");
                ImGui.Spacing();

                if (ImGui.Button("Yes, Delete"))
                {
                    var plugins = _updater?.GetPluginInfo() ?? new List<PluginInfo>();
                    var pluginToDelete = plugins.FirstOrDefault(p => p.Name.Equals(_pluginToDelete, StringComparison.OrdinalIgnoreCase));
                    
                    if (pluginToDelete != null)
                    {
                        _ = DeletePluginAsync(pluginToDelete);
                    }
                    
                    _showDeleteConfirmation = false;
                    _pluginToDelete = "";
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel"))
                {
                    _showDeleteConfirmation = false;
                    _pluginToDelete = "";
                }

                ImGui.EndPopup();
            }
        }

        private void RenderUpdateButtons()
        {
            if (_isUpdating)
            {
                ImGui.BeginDisabled();
                string progressText = _totalProgress > 0
                    ? $"Checking For Updates... ({_currentProgress}/{_totalProgress})"
                    : "Checking For Updates...";
                ImGui.Button(progressText);
                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button("Check For Updates"))
                {
                    _currentProgress = 0;
                    _totalProgress = 0;
                    _ = UpdateGitInfoAsync();
                }

                ImGui.SameLine();

                if (ImGui.Button("Refresh (local)"))
                {
                    _updater?.UpdateLocal();
                }

                var plugins = _updater?.GetPluginInfo() ?? new List<PluginInfo>();
                bool hasUpdates = plugins.Any(p => p.CurrentCommit != p.LatestCommit);

                if (hasUpdates)
                {
                    ImGui.SameLine();

                    if (_isUpdatingAll)
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button("Updating All...");
                        ImGui.EndDisabled();
                    }
                    else
                    {
                        if (ImGui.Button("Update All"))
                        {
                            _ = UpdateAllPluginsAsync();
                        }
                    }
                }
            }

            // Show progress if updating
            if (_isUpdating && _totalProgress > 0)
            {
                float progress = (float)_currentProgress / _totalProgress;
                ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 2));
            }
        }

        private void RenderPluginsTable(float availableHeight = -1)
        {
            var tableFlags = ImGuiTableFlags.Borders |
                             ImGuiTableFlags.Resizable |
                             ImGuiTableFlags.SizingFixedFit |
                             ImGuiTableFlags.ScrollY |
                             ImGuiTableFlags.ScrollX |
                             ImGuiTableFlags.RowBg;

            var plugins = _updater?.GetPluginInfo() ?? new List<PluginInfo>();

            float rowHeight = Math.Max(
                ImGui.GetTextLineHeightWithSpacing(),
                ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.Y * 2
            );

            // Se não foi especificada altura, usar toda a região disponível
            float tableHeight;
            if (availableHeight > 0)
            {
                tableHeight = availableHeight;
            }
            else
            {
                float totalTableHeight = rowHeight * (plugins.Count + 1);
                float panelHeight = ImGui.GetContentRegionAvail().Y;
                tableHeight = Math.Min(totalTableHeight, panelHeight * 0.90f); // 90% ao invés de 60%
            }

            if (!ImGui.BeginTable("##pluginsTable", 4, tableFlags, new System.Numerics.Vector2(-1, tableHeight)))
                return;

            SetupTableColumns();
            ImGui.TableHeadersRow();
            RenderTableRows();
            ImGui.EndTable();
        }

        private static void SetupTableColumns()
        {
            // Configurar colunas com larguras mais adequadas
            ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthFixed, 150.0f);
            ImGui.TableSetupColumn("Branch", ImGuiTableColumnFlags.WidthFixed, 100.0f);
            ImGui.TableSetupColumn("Commit", ImGuiTableColumnFlags.WidthStretch); // Esta vai ocupar o espaço restante
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 100.0f);
        }

        private void RenderTableRows()
        {
            var plugins = _updater?.GetPluginInfo() ?? new List<PluginInfo>();

            foreach (var pluginInfo in plugins
                         .Where(x => string.IsNullOrEmpty(_pluginNameFilter) || 
                                    x.Name.Contains(_pluginNameFilter, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                try
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    RenderPluginName(pluginInfo);
                    ImGui.TableNextColumn();
                    RenderBranchInfo(pluginInfo);
                    ImGui.TableNextColumn();
                    RenderCommitInfo(pluginInfo);
                    ImGui.TableNextColumn();
                    RenderActionButtons(pluginInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error rendering plugin {pluginInfo.Name}: {e.Message}");
                }
            }
        }

        private static void RenderPluginName(PluginInfo pluginInfo)
        {
            ImGui.Text(pluginInfo.Name);
        }

        private static void RenderBranchInfo(PluginInfo pluginInfo)
        {
            ImGui.Text(pluginInfo.CurrentBranch);
        }

        private static void RenderCommitInfo(PluginInfo pluginInfo)
        {
            if (!string.IsNullOrEmpty(pluginInfo.Error))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Update failed");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(pluginInfo.Error);
                }
                return;
            }

            string text;
            if (string.IsNullOrEmpty(pluginInfo.LatestCommit))
            {
                text = "No commit information";
            }
            else if (pluginInfo.CurrentCommit == pluginInfo.LatestCommit)
            {
                text = $"Currently on latest commit ({pluginInfo.CurrentCommit})";
            }
            else
            {
                text = $"{pluginInfo.CurrentCommit} -> {pluginInfo.LatestCommit} '{pluginInfo.LatestCommitMessage}' ({pluginInfo.BehindAhead})";
            }

            if (pluginInfo.UncommittedChangeCount > 0)
            {
                text += $" ({pluginInfo.UncommittedChangeCount} uncommitted changes)";
            }

            ImGui.TextWrapped(text);
        }

        private void RenderActionButtons(PluginInfo pluginInfo)
        {
            if (!string.IsNullOrEmpty(pluginInfo.CurrentCommit))
            {
                bool isUpdating = _updatingPlugins.TryGetValue(pluginInfo.Name, out bool updating) && updating;

                if (pluginInfo.CurrentCommit != pluginInfo.LatestCommit)
                {
                    ImGui.BeginDisabled(isUpdating);
                    if (pluginInfo.AheadBy == 0 || pluginInfo.BehindBy != 0)
                    {
                        if (ImGui.Button(isUpdating ? $"Updating...##{pluginInfo.Name}" : $"Update##{pluginInfo.Name}"))
                        {
                            _ = UpdatePluginAsync(pluginInfo.Name);
                        }
                        ImGui.SameLine();
                    }
                    ImGui.EndDisabled();
                }
            }
            
            // Botão Rebuild sempre disponível para plugins com código fonte
            if (!string.IsNullOrEmpty(pluginInfo.Path) && Directory.Exists(pluginInfo.Path))
            {
                bool isBuilding = _updatingPlugins.TryGetValue($"build_{pluginInfo.Name}", out bool building) && building;
                
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 0.8f, 1.0f)); // Azul
                ImGui.BeginDisabled(isBuilding);
                if (ImGui.Button(isBuilding ? $"Building...##{pluginInfo.Name}" : $"Rebuild##{pluginInfo.Name}"))
                {
                    _ = RebuildPluginAsync(pluginInfo.Name);
                }
                ImGui.EndDisabled();
                ImGui.PopStyleColor();
                ImGui.SameLine();
            }
            
            // Botão Delete sempre disponível
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1.0f)); // Vermelho
            if (ImGui.Button($"Delete##{pluginInfo.Name}"))
            {
                _pluginToDelete = pluginInfo.Name;
                _showDeleteConfirmation = true;
            }
            ImGui.PopStyleColor();
        }

        private void RenderAddPluginSection()
        {
            ImGui.TextWrapped("Enter the URL of a Git repository to clone. " +
                              "The repository will be cloned into the Plugins folder.");

            ImGui.InputText("Repository URL", ref _repoUrl, 500);

            if (_isCloning)
            {
                ImGui.BeginDisabled();
                ImGui.Button("Cloning...");
                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button("Clone Repository"))
                {
                    if (!string.IsNullOrWhiteSpace(_repoUrl))
                    {
                        _ = CloneRepositoryAsync(_repoUrl);
                    }
                }
            }
        }

        private async Task UpdateGitInfoAsync()
        {
            if (_isUpdating || _updater == null) return;

            try
            {
                _isUpdating = true;
                await _updater.UpdateGitInfoAsync();

                var plugins = _updater.GetPluginInfo();
                int updateCount = plugins.Count(p => p.CurrentCommit != p.LatestCommit);
                if (updateCount > 0)
                {
                    Console.WriteLine($"There are {updateCount} plugin updates pending.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error updating git info: {e}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task UpdatePluginAsync(string pluginName)
        {
            if (_updatingPlugins.TryGetValue(pluginName, out bool isUpdating) && isUpdating)
                return;

            try
            {
                _updatingPlugins[pluginName] = true;
                Console.WriteLine($"Starting update for {pluginName}...");
                
                await _updater.UpdatePluginAsync(pluginName);
                Console.WriteLine($"Successfully updated {pluginName}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error updating plugin {pluginName}: {e.Message}");
            }
            finally
            {
                _updatingPlugins[pluginName] = false;
            }
        }

        private async Task UpdateAllPluginsAsync()
        {
            if (_isUpdatingAll || _updater == null) return;

            try
            {
                _isUpdatingAll = true;
                var plugins = _updater.GetPluginInfo();
                var outdatedPlugins = plugins.Where(p => p.CurrentCommit != p.LatestCommit).ToList();

                Console.WriteLine($"Starting update for {outdatedPlugins.Count} plugins...");

                foreach (var plugin in outdatedPlugins)
                {
                    await UpdatePluginAsync(plugin.Name);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error updating all plugins: {e.Message}");
            }
            finally
            {
                _isUpdatingAll = false;
            }
        }

        private async Task CloneRepositoryAsync(string repoUrl)
        {
            if (_isCloning || _updater == null) return;

            try
            {
                _isCloning = true;
                Console.WriteLine($"Cloning repository: {repoUrl}");
                
                await _updater.CloneRepositoryAsync(repoUrl);
                
                // Refresh the plugin list
                var repoName = ExtractRepoName(repoUrl);
                var plugins = _updater.GetPluginInfo();
                var newPlugin = plugins.FirstOrDefault(p => p.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));
                
                _repoUrl = "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cloning repository: {ex.Message}");
            }
            finally
            {
                _isCloning = false;
            }
        }

        private void RestartApplication()
        {
            try
            {
                Console.WriteLine("🔄 Restarting GameHelper application...");
                
                // Use the async method to handle the restart properly
                _ = ReloadAllPluginsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initiating restart: {ex.Message}");
            }
        }

        private string ExtractRepoName(string repoUrl)
        {
            try
            {
                var uri = new Uri(repoUrl);
                var segments = uri.Segments;
                return segments[^1].Replace(".git", "");
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task RebuildPluginAsync(string pluginName)
        {
            if (_updater == null) return;
            
            try
            {
                _updatingPlugins[$"build_{pluginName}"] = true;
                Console.WriteLine($"Rebuilding plugin {pluginName}...");
                
                var plugins = _updater.GetPluginInfo();
                var plugin = plugins.FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
                
                if (plugin != null && !string.IsNullOrEmpty(plugin.Path) && Directory.Exists(plugin.Path))
                {
                    await _updater.BuildAndDeployPluginAsync(plugin.Path, pluginName);
                    Console.WriteLine($"Plugin {pluginName} rebuilt successfully!");
                }
                else
                {
                    Console.WriteLine($"Plugin {pluginName} source not found for rebuild");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rebuilding plugin {pluginName}: {ex.Message}");
            }
            finally
            {
                _updatingPlugins[$"build_{pluginName}"] = false;
            }
        }

        private async Task ReloadAllPluginsAsync()
        {
            if (_isUpdating || _updater == null) return;

            try
            {
                _isUpdating = true;
                Console.WriteLine("🔄 Restarting GameHelper to reload all plugins...");

                // Wait a moment to ensure current operations complete
                await Task.Delay(500);
                
                // Get the current GameHelper executable path
                var currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                
                if (!string.IsNullOrEmpty(currentExePath) && File.Exists(currentExePath))
                {
                    Console.WriteLine($"🔄 Starting new GameHelper process: {currentExePath}");
                    
                    // Start the new GameHelper process
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = currentExePath,
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(currentExePath)
                    };
                    
                    System.Diagnostics.Process.Start(startInfo);
                    
                    // Wait longer for the new process to start and old one to fully terminate
                    await Task.Delay(2000);
                    
                    Console.WriteLine("� New process started, closing current GameHelper...");
                    
                    // Safely close the current GameHelper using the standard mechanism
                    Core.GHSettings.IsOverlayRunning = false;
                }
                else
                {
                    Console.WriteLine("❌ Could not find GameHelper executable path for restart");
                    Console.WriteLine("📌 Falling back to plugin information refresh...");
                    
                    // Fall back to just refreshing plugin info
                    _updater.UpdateLocal();
                    await _updater.UpdateGitInfoAsync();
                    Console.WriteLine("✅ Plugin information refreshed!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during restart: {ex.Message}");
                Console.WriteLine("📌 Falling back to plugin information refresh...");
                
                try
                {
                    // Fall back to just refreshing plugin info
                    _updater.UpdateLocal();
                    await _updater.UpdateGitInfoAsync();
                    Console.WriteLine("✅ Plugin information refreshed!");
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"❌ Error refreshing plugin information: {fallbackEx.Message}");
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task BuildAllPluginsAsync()
        {
            var plugins = _updater?.GetPluginInfo() ?? new List<PluginInfo>();
            
            foreach (var plugin in plugins)
            {
                try
                {
                    await BuildPluginAsync(plugin);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error building plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        private async Task BuildPluginAsync(PluginInfo plugin)
        {
            if (string.IsNullOrEmpty(plugin.Path) || !Directory.Exists(plugin.Path))
                return;

            // Procurar por arquivo .csproj na pasta do plugin
            var csprojFiles = Directory.GetFiles(plugin.Path, "*.csproj", SearchOption.TopDirectoryOnly);
            
            if (csprojFiles.Length == 0)
            {
                Console.WriteLine($"No .csproj file found for plugin {plugin.Name}");
                return;
            }

            var csprojPath = csprojFiles[0];
            Console.WriteLine($"Building plugin {plugin.Name} from {csprojPath}");

            await Task.Run(() =>
            {
                try
                {
                    // Primeiro limpar builds anteriores
                    var cleanProcessInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"clean \"{csprojPath}\" -c Release",
                        WorkingDirectory = plugin.Path,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var cleanProcess = Process.Start(cleanProcessInfo))
                    {
                        cleanProcess?.WaitForExit(15000);
                    }

                    // Agora fazer o build
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{csprojPath}\" -c Release --output \"{plugin.Path}\\bin\\Release\"",
                        WorkingDirectory = plugin.Path,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        process.WaitForExit(30000); // 30 seconds timeout
                        
                        if (process.ExitCode == 0)
                        {
                            Console.WriteLine($"Successfully built plugin {plugin.Name}");
                            
                            // Tentar copiar a DLL para o local correto se necessário
                            CopyPluginDllToCorrectLocation(plugin);
                        }
                        else
                        {
                            var error = process.StandardError.ReadToEnd();
                            var output = process.StandardOutput.ReadToEnd();
                            Console.WriteLine($"Failed to build plugin {plugin.Name}:");
                            Console.WriteLine($"Error: {error}");
                            Console.WriteLine($"Output: {output}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception building plugin {plugin.Name}: {ex.Message}");
                }
            });
        }

        private void CopyPluginDllToCorrectLocation(PluginInfo plugin)
        {
            try
            {
                var buildOutputPath = Path.Combine(plugin.Path, "bin", "Release");
                if (!Directory.Exists(buildOutputPath))
                {
                    // Tentar net8.0 específico
                    buildOutputPath = Path.Combine(plugin.Path, "bin", "Release", "net8.0");
                }

                if (!Directory.Exists(buildOutputPath))
                {
                    Console.WriteLine($"Build output directory not found for {plugin.Name}");
                    return;
                }

                // Procurar por DLLs no diretório de output
                var dllFiles = Directory.GetFiles(buildOutputPath, "*.dll", SearchOption.TopDirectoryOnly);
                var targetDll = dllFiles.FirstOrDefault(dll => 
                    Path.GetFileNameWithoutExtension(dll).Equals(plugin.Name, StringComparison.OrdinalIgnoreCase));

                if (targetDll != null)
                {
                    var destPath = Path.Combine(plugin.Path, $"{plugin.Name}.dll");
                    File.Copy(targetDll, destPath, true);
                    Console.WriteLine($"Copied {plugin.Name}.dll to plugin directory");
                }
                else
                {
                    Console.WriteLine($"Could not find {plugin.Name}.dll in build output");
                    // Listar todas as DLLs encontradas para debug
                    Console.WriteLine($"Available DLLs: {string.Join(", ", dllFiles.Select(Path.GetFileName))}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying DLL for plugin {plugin.Name}: {ex.Message}");
            }
        }

        private async Task DeletePluginAsync(PluginInfo pluginInfo)
        {
            if (string.IsNullOrEmpty(pluginInfo.Path) || !Directory.Exists(pluginInfo.Path))
            {
                Console.WriteLine($"Plugin path not found for {pluginInfo.Name}");
                return;
            }

            try
            {
                Console.WriteLine($"Deleting plugin {pluginInfo.Name}...");

                await Task.Run(() =>
                {
                    try
                    {
                        // Get both source and compiled paths
                        var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                        var gameHelperPath = Path.Combine(currentPath, "..", "..");
                        gameHelperPath = Path.GetFullPath(gameHelperPath);
                        
                        var sourceDir = Path.Combine(gameHelperPath, "PluginSources");
                        var pluginDir = Path.Combine(gameHelperPath, "Plugins");
                        
                        var sourcePath = Path.Combine(sourceDir, pluginInfo.Name);
                        var compiledPath = Path.Combine(pluginDir, pluginInfo.Name);
                        
                        // Delete from PluginSources (Git repo) - this is always safe
                        if (Directory.Exists(sourcePath))
                        {
                            Console.WriteLine($"🗂️ Deleting source code: {sourcePath}");
                            ForceDeleteDirectory(sourcePath);
                            Console.WriteLine($"✅ Source code deleted successfully");
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️ No source directory found");
                        }
                        
                        // Delete compiled plugin with force unlock
                        if (Directory.Exists(compiledPath))
                        {
                            Console.WriteLine($"📦 Deleting compiled plugin: {compiledPath}");
                            
                            if (ForceDeleteDirectory(compiledPath))
                            {
                                Console.WriteLine($"✅ Plugin {pluginInfo.Name} deleted successfully!");
                            }
                            else
                            {
                                Console.WriteLine($"❌ Could not delete plugin completely");
                                Console.WriteLine($"💡 Try closing GameHelper completely and deleting manually:");
                                Console.WriteLine($"    Folder: {compiledPath}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️ No compiled directory found");
                        }
                        if (Directory.Exists(compiledPath))
                        {
                            try
                            {
                                Console.WriteLine($"📦 Processing compiled plugin at: {compiledPath}");
                                
                                // Create temp directory for deleted plugins
                                var tempDeletePath = Path.Combine(gameHelperPath, "temp_delete");
                                Directory.CreateDirectory(tempDeletePath);
                                
                                // Generate unique temp name with timestamp
                                var tempPluginPath = Path.Combine(tempDeletePath, $"{pluginInfo.Name}_{DateTime.Now:yyyyMMdd_HHmmss}");
                                
                                Console.WriteLine($"🔄 Moving plugin to temp location to avoid file locks...");
                                Console.WriteLine($"📁 Target temp path: {tempPluginPath}");
                                
                                // Move instead of delete - this works even with loaded DLLs
                                Directory.Move(compiledPath, tempPluginPath);
                                
                                Console.WriteLine($"✅ Plugin {pluginInfo.Name} successfully moved to temp location");
                                
                                // Schedule background cleanup
                                _ = Task.Run(async () =>
                                {
                                    Console.WriteLine($"⏳ Starting background cleanup for {pluginInfo.Name} in 5 seconds...");
                                    await Task.Delay(5000); // Wait 5 seconds
                                    await TryDeleteTempDirectoryAsync(tempPluginPath, pluginInfo.Name);
                                });
                                
                                Console.WriteLine($"🧹 Background cleanup scheduled for {pluginInfo.Name}");
                            }
                            catch (Exception moveEx)
                            {
                                Console.WriteLine($"❌ Could not move plugin: {moveEx.Message}");
                                Console.WriteLine($"� This usually means the plugin files are currently in use.");
                                Console.WriteLine($"�📝 Manual deletion instructions:");
                                Console.WriteLine($"   1. Close GameHelper completely");
                                Console.WriteLine($"   2. Delete folder: {compiledPath}");
                                Console.WriteLine($"   3. Restart GameHelper");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️ No compiled directory found for {pluginInfo.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during plugin deletion: {ex.Message}");
                    }
                });

                // Atualizar a lista local após operação
                _updater?.UpdateLocal();
                Console.WriteLine($"✅ Plugin {pluginInfo.Name} deletion process completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete plugin {pluginInfo.Name}: {ex.Message}");
            }
        }

        private async Task TryDeleteTempDirectoryAsync(string tempPath, string pluginName)
        {
            try
            {
                Console.WriteLine($"🧹 [Background] Starting cleanup of temp files for {pluginName}...");
                await Task.Delay(1000); // Additional delay
                
                if (Directory.Exists(tempPath))
                {
                    Console.WriteLine($"🗑️ [Background] Attempting to delete: {tempPath}");
                    
                    // Try to delete the temp directory
                    RemoveReadOnlyAttributes(tempPath);
                    Directory.Delete(tempPath, true);
                    Console.WriteLine($"✅ [Background] Successfully cleaned up temp files for {pluginName}");
                }
                else
                {
                    Console.WriteLine($"ℹ️ [Background] Temp directory already cleaned for {pluginName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [Background] Could not clean up temp files for {pluginName}: {ex.Message}");
                Console.WriteLine($"📁 Temp files remain at: {tempPath}");
                Console.WriteLine($"💡 These files will be cleaned up automatically on next GameHelper startup,");
                Console.WriteLine($"    or you can manually delete the temp_delete folder when GameHelper is closed.");
            }
        }

        /// <summary>
        /// Force delete a directory even if files are in use
        /// </summary>
        private bool ForceDeleteDirectory(string directoryPath)
        {
            try
            {
                // First try: Simple delete
                if (TrySimpleDelete(directoryPath))
                {
                    Console.WriteLine($"✅ Simple delete successful");
                    return true;
                }

                Console.WriteLine($"⚠️ Simple delete failed, trying force methods...");

                // Second try: Remove read-only and try again
                RemoveReadOnlyAttributes(directoryPath);
                if (TrySimpleDelete(directoryPath))
                {
                    Console.WriteLine($"✅ Delete after removing read-only successful");
                    return true;
                }

                // Third try: Individual file deletion
                Console.WriteLine($"📝 Attempting individual file deletion...");
                return ForceDeleteFilesIndividually(directoryPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Force delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try simple directory delete
        /// </summary>
        private bool TrySimpleDelete(string directoryPath)
        {
            try
            {
                Directory.Delete(directoryPath, true);
                return !Directory.Exists(directoryPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Force delete files individually
        /// </summary>
        private bool ForceDeleteFilesIndividually(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return true;

                bool success = true;
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                
                Console.WriteLine($"🗃️ Attempting to delete {files.Length} files individually...");
                
                foreach (var file in files)
                {
                    if (!ForceDeleteFile(file))
                        success = false;
                }

                // Try to remove empty directories
                try
                {
                    Directory.Delete(directoryPath, true);
                }
                catch
                {
                    success = false;
                }

                return success && !Directory.Exists(directoryPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Force delete a single file
        /// </summary>
        private bool ForceDeleteFile(string filePath)
        {
            try
            {
                // Remove read-only
                File.SetAttributes(filePath, FileAttributes.Normal);
                
                // Try to delete
                File.Delete(filePath);
                
                return !File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Could not delete {Path.GetFileName(filePath)}: {ex.Message}");
                return false;
            }
        }

        private void RemoveReadOnlyAttributes(string directoryPath)
        {
            try
            {
                // Remove readonly de todos os arquivos recursivamente
                foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not change attributes for {file}: {ex.Message}");
                    }
                }

                // Remove readonly de todos os diretórios
                foreach (string dir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(dir, FileAttributes.Normal);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not change attributes for {dir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing readonly attributes: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the store with known plugin repositories.
        /// </summary>
        private void InitializeStore()
        {
            _storePlugins.Clear();
            
            // Add known repositories
            _storePlugins.Add(new StorePluginInfo
            {
                Name = "Atlas",
                RepositoryUrl = "https://github.com/arsenic2k/Atlas",
                Description = "Advanced mapping and exploration plugin with enhanced features for Path of Exile",
                Author = "arsenic2k",
                Category = "Mapping"
            });

            _storePlugins.Add(new StorePluginInfo
            {
                Name = "Tracker",
                RepositoryUrl = "https://github.com/arsenic2k/Tracker",
                Description = "Comprehensive tracking system for game events and statistics",
                Author = "arsenic2k", 
                Category = "Utility"
            });

            // Update installation status
            UpdateStoreInstallationStatus();
        }

        /// <summary>
        /// Updates the installation status of store plugins.
        /// </summary>
        private void UpdateStoreInstallationStatus()
        {
            if (_updater == null) return;

            var installedPlugins = _updater.GetPluginInfo();
            
            foreach (var storePlugin in _storePlugins)
            {
                storePlugin.IsInstalled = installedPlugins.Any(p => 
                    p.Name.Equals(storePlugin.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Renders the store section with available plugins.
        /// </summary>
        private void RenderStoreSection()
        {
            ImGui.Text("Plugin Store - Browse and install community plugins");
            ImGui.Separator();
            
            // Search filter
            ImGui.Text("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##storeSearch", ref _storeSearchFilter, 256);
            ImGui.Spacing();

            // Refresh button
            if (ImGui.Button("Refresh Store", new System.Numerics.Vector2(120, 30)))
            {
                UpdateStoreInstallationStatus();
            }
            ImGui.Spacing();

            // Store plugins table
            var tableFlags = ImGuiTableFlags.Borders |
                             ImGuiTableFlags.Resizable |
                             ImGuiTableFlags.SizingFixedFit |
                             ImGuiTableFlags.ScrollY |
                             ImGuiTableFlags.RowBg;

            var availableHeight = ImGui.GetContentRegionAvail().Y - 10;
            
            if (ImGui.BeginTable("##storeTable", 5, tableFlags, new System.Numerics.Vector2(-1, availableHeight)))
            {
                // Setup columns
                ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthFixed, 120.0f);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, 80.0f);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                ImGui.TableHeadersRow();

                // Filter and display plugins
                var filteredPlugins = _storePlugins.Where(p => 
                    string.IsNullOrEmpty(_storeSearchFilter) ||
                    p.Name.Contains(_storeSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(_storeSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    p.Author.Contains(_storeSearchFilter, StringComparison.OrdinalIgnoreCase));

                foreach (var plugin in filteredPlugins)
                {
                    ImGui.TableNextRow();

                    // Plugin name
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(plugin.Name);

                    // Author
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(plugin.Author);

                    // Category
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(plugin.Category);

                    // Description
                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextWrapped(plugin.Description);

                    // Action button
                    ImGui.TableSetColumnIndex(4);
                    
                    if (plugin.IsInstalled)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Installed");
                    }
                    else if (plugin.IsInstalling)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Installing...");
                    }
                    else
                    {
                        var buttonId = $"Install##{plugin.Name}";
                        if (ImGui.Button(buttonId, new System.Numerics.Vector2(80, 25)))
                        {
                            InstallStorePlugin(plugin);
                        }
                        
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Install {plugin.Name} from {plugin.RepositoryUrl}");
                        }
                    }
                }

                ImGui.EndTable();
            }
        }

        /// <summary>
        /// Installs a plugin from the store.
        /// </summary>
        private async void InstallStorePlugin(StorePluginInfo storePlugin)
        {
            if (_isCloning || storePlugin.IsInstalling) return;

            try
            {
                storePlugin.IsInstalling = true;
                _isCloning = true;

                Console.WriteLine($"🏪 Installing plugin from store: {storePlugin.Name}");
                Console.WriteLine($"🔗 Repository: {storePlugin.RepositoryUrl}");

                await _updater.CloneRepositoryAsync(storePlugin.RepositoryUrl);
                
                Console.WriteLine($"✅ Store plugin {storePlugin.Name} installed successfully!");
                
                // Update installation status
                UpdateStoreInstallationStatus();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to install store plugin {storePlugin.Name}: {ex.Message}");
            }
            finally
            {
                storePlugin.IsInstalling = false;
                _isCloning = false;
            }
        }

        /// <summary>
        /// Disposes the renderer.
        /// </summary>
        public void Dispose()
        {
            _updater?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}