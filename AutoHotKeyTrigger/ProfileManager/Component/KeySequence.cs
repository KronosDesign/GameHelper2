// <copyright file="KeySequence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutoHotKeyTrigger.ProfileManager.Component
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;

    /// <summary>
    ///     Manages a sequence of key actions with delays between them
    /// </summary>
    public class KeySequence
    {
        [JsonProperty]
        private readonly List<KeyAction> actions = new();

        private readonly object executionLock = new object();
        private bool isExecuting = false;

        /// <summary>
        ///     Whether this sequence is currently executing
        /// </summary>
        [JsonIgnore]
        public bool IsExecuting 
        { 
            get 
            { 
                lock (executionLock) 
                { 
                    return isExecuting; 
                } 
            } 
        }

        /// <summary>
        ///     Number of actions in the sequence
        /// </summary>
        [JsonIgnore]
        public int Count => actions.Count;

        /// <summary>
        ///     Whether sequence has any actions
        /// </summary>
        [JsonIgnore]
        public bool HasActions => actions.Count > 0;

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeySequence"/> class.
        /// </summary>
        public KeySequence()
        {
            actions = new List<KeyAction>();
        }

        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="other">KeySequence to copy from</param>
        public KeySequence(KeySequence other)
        {
            actions = new List<KeyAction>();
            foreach (var action in other.actions)
            {
                actions.Add(new KeyAction(action));
            }
        }

        /// <summary>
        ///     Adds a key action to the sequence
        /// </summary>
        /// <param name="key">The key to press</param>
        /// <param name="delayMs">Delay before pressing this key (in milliseconds)</param>
        public void AddAction(VK key, int delayMs = 0)
        {
            actions.Add(new KeyAction(key, delayMs));
        }

        /// <summary>
        ///     Adds a key action to the sequence
        /// </summary>
        /// <param name="action">The key action to add</param>
        public void AddAction(KeyAction action)
        {
            actions.Add(new KeyAction(action));
        }

        /// <summary>
        ///     Removes an action at the specified index
        /// </summary>
        /// <param name="index">Index of action to remove</param>
        public void RemoveAction(int index)
        {
            if (index >= 0 && index < actions.Count)
            {
                actions.RemoveAt(index);
            }
        }

        /// <summary>
        ///     Clears all actions from the sequence
        /// </summary>
        public void Clear()
        {
            StopExecution();
            actions.Clear();
        }

        /// <summary>
        ///     Gets an action at the specified index
        /// </summary>
        /// <param name="index">Index of action to get</param>
        /// <returns>KeyAction at the specified index, or null if index is invalid</returns>
        public KeyAction GetAction(int index)
        {
            if (index >= 0 && index < actions.Count)
            {
                return actions[index];
            }
            return null;
        }

        /// <summary>
        ///     Executes the key sequence synchronously with delays
        /// </summary>
        /// <param name="logger">Logger function for debug messages</param>
        public void Execute(Action<string> logger)
        {
            lock (executionLock)
            {
                if (isExecuting)
                {
                    logger("Key sequence is already executing, skipping...");
                    return;
                }
                isExecuting = true;
            }

            try
            {
                logger($"Starting key sequence execution with {actions.Count} actions");
                
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    
                    logger($"Processing action {i + 1}/{actions.Count}: Key={action.Key}, Delay={action.DelayMs}ms");
                    
                    // Wait for the delay before pressing the key
                    if (action.DelayMs > 0)
                    {
                        logger($"Waiting {action.DelayMs}ms before pressing {action.Key}");
                        Thread.Sleep(action.DelayMs);
                        logger($"Finished waiting {action.DelayMs}ms, now pressing {action.Key}");
                    }

                    // Press the key
                    logger($"Attempting to press {action.Key}...");
                    bool keyPressed = MiscHelper.KeyUp(action.Key);
                    
                    if (keyPressed)
                    {
                        logger($"SUCCESS: Pressed {action.Key} (action {i + 1}/{actions.Count})");
                    }
                    else
                    {
                        logger($"FAILED: Could not press {action.Key} (action {i + 1}/{actions.Count})");
                    }
                    
                    // Small delay between keys to ensure they register properly
                    Thread.Sleep(50);
                }
                
                logger($"Completed key sequence execution");
            }
            catch (Exception ex)
            {
                logger($"ERROR during key sequence execution: {ex.Message}");
                logger($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                lock (executionLock)
                {
                    isExecuting = false;
                }
                logger("Key sequence execution finished, lock released");
            }
        }

        /// <summary>
        ///     Stops the current execution if running
        /// </summary>
        public void StopExecution()
        {
            lock (executionLock)
            {
                isExecuting = false;
            }
        }

        /// <summary>
        ///     Draws the ImGui settings for this key sequence
        /// </summary>
        public void DrawSettings()
        {
            ImGui.Text($"Key Sequence ({actions.Count} actions)");
            
            if (ImGui.Button("Add Key Action"))
            {
                actions.Add(new KeyAction());
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear All Actions"))
            {
                Clear();
            }

            if (IsExecuting)
            {
                ImGui.SameLine();
                if (ImGui.Button("Stop Execution"))
                {
                    StopExecution();
                }
                ImGui.Text("(Currently executing...)");
            }

            ImGui.Separator();

            // Draw each action
            for (int i = 0; i < actions.Count; i++)
            {
                ImGui.PushID(i);
                
                var action = actions[i];
                var key = action.Key;
                var delay = action.DelayMs;

                ImGui.Text($"Action {i + 1}:");
                ImGui.SameLine();
                
                if (ImGui.Button("Remove"))
                {
                    RemoveAction(i);
                    ImGui.PopID();
                    break;
                }

                // Key selection
                ImGui.SetNextItemWidth(120);
                if (ImGuiHelper.NonContinuousEnumComboBox($"##key{i}", ref key))
                {
                    action.Key = key;
                }

                ImGui.SameLine();
                ImGui.Text("Delay:");
                ImGui.SameLine();
                
                // Delay input
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt($"##delay{i}", ref delay, 50, 100))
                {
                    action.DelayMs = Math.Max(0, delay); // Ensure non-negative delay
                }
                
                ImGui.SameLine();
                ImGui.Text("ms");

                ImGui.PopID();
            }

            if (actions.Count == 0)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No actions configured");
            }
        }

        /// <summary>
        ///     Gets a summary string of the sequence for display purposes
        /// </summary>
        /// <returns>Summary string</returns>
        public string GetSummary()
        {
            if (actions.Count == 0)
            {
                return "No actions";
            }

            var summary = string.Join(" → ", actions.Select(a => 
            {
                if (a.DelayMs > 0)
                {
                    return $"{a.Key}({a.DelayMs}ms)";
                }
                return a.Key.ToString();
            }));

            return summary;
        }
    }
}