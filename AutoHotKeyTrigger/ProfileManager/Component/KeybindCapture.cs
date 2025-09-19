// <copyright file="KeybindCapture.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutoHotKeyTrigger.ProfileManager.Component
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Utils;
    using ImGuiNET;

    /// <summary>
    ///     Helper class for capturing key combinations from user input
    /// </summary>
    public static class KeybindCapture
    {
        private static readonly Dictionary<int, KeyCaptureState> captureStates = new();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        ///     Represents the state of a key capture session
        /// </summary>
        private class KeyCaptureState
        {
            public bool IsCapturing { get; set; }
            public bool CapturedCtrl { get; set; }
            public bool CapturedAlt { get; set; }
            public bool CapturedShift { get; set; }
            public bool CapturedWin { get; set; }
            public VK CapturedKey { get; set; } = VK.KEY_1; // Default to a valid key
            public bool HasCapturedKey { get; set; }
            public DateTime LastCaptureTime { get; set; }
        }

        /// <summary>
        ///     Draws a keybind capture interface
        /// </summary>
        /// <param name="id">Unique identifier for this capture session</param>
        /// <param name="currentAction">Current KeyAction to modify</param>
        /// <param name="buttonText">Text to show on the capture button</param>
        /// <returns>True if the keybind was updated</returns>
        public static bool DrawKeybindCapture(int id, KeyAction currentAction, string buttonText = null)
        {
            if (buttonText == null)
                buttonText = $"Set Keybind: {currentAction.GetDisplayString()}";

            bool updated = false;

            // Initialize capture state if needed
            if (!captureStates.ContainsKey(id))
            {
                captureStates[id] = new KeyCaptureState();
            }

            var state = captureStates[id];

            // Capture button
            if (ImGui.Button($"{buttonText}##capture{id}"))
            {
                ImGui.OpenPopup($"KeybindCapture{id}");
                state.IsCapturing = false; // Reset capturing state when popup opens
            }

            // Keybind capture popup
            if (ImGui.BeginPopup($"KeybindCapture{id}"))
            {
                ImGui.Text("Key Binding Setup");
                ImGui.Separator();

                // Capture mode toggle
                if (ImGui.Button(state.IsCapturing ? "Stop Capturing" : "Start Capturing"))
                {
                    if (!state.IsCapturing)
                    {
                        // Start capturing
                        state.IsCapturing = true;
                        state.CapturedCtrl = false;
                        state.CapturedAlt = false;
                        state.CapturedShift = false;
                        state.CapturedWin = false;
                        state.CapturedKey = VK.KEY_1;
                        state.HasCapturedKey = false;
                        state.LastCaptureTime = DateTime.Now;
                    }
                    else
                    {
                        state.IsCapturing = false;
                    }
                }

                if (state.IsCapturing)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Capturing... Press your key combination!");
                    ImGui.Text("Press ESC to cancel");

                    // Check for key presses during capture
                    CheckForKeyCapture(state);

                    // Show what's been captured so far
                    var capturedKeys = new List<string>();
                    if (state.CapturedCtrl) capturedKeys.Add("CTRL");
                    if (state.CapturedAlt) capturedKeys.Add("ALT");
                    if (state.CapturedShift) capturedKeys.Add("SHIFT");
                    if (state.CapturedWin) capturedKeys.Add("WIN");
                    if (state.HasCapturedKey && state.CapturedKey != VK.KEY_1)
                    {
                        capturedKeys.Add(state.CapturedKey.ToString().Replace("KEY_", ""));
                    }

                    if (capturedKeys.Count > 0)
                    {
                        ImGui.Text($"Captured: {string.Join(" + ", capturedKeys)}");
                    }

                    // Auto-stop capturing after getting a main key
                    if (state.HasCapturedKey && state.CapturedKey != VK.KEY_1)
                    {
                        state.IsCapturing = false;
                    }
                }
                else if (state.HasCapturedKey)
                {
                    // Show captured result
                    var capturedKeys = new List<string>();
                    if (state.CapturedCtrl) capturedKeys.Add("CTRL");
                    if (state.CapturedAlt) capturedKeys.Add("ALT");
                    if (state.CapturedShift) capturedKeys.Add("SHIFT");
                    if (state.CapturedWin) capturedKeys.Add("WIN");
                    if (state.CapturedKey != VK.KEY_1)
                    {
                        capturedKeys.Add(state.CapturedKey.ToString().Replace("KEY_", ""));
                    }

                    ImGui.TextColored(new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f), 
                        $"Captured: {string.Join(" + ", capturedKeys)}");

                    if (ImGui.Button("Apply This Keybind"))
                    {
                        // Apply the captured keybind
                        currentAction.UseCtrl = state.CapturedCtrl;
                        currentAction.UseAlt = state.CapturedAlt;
                        currentAction.UseShift = state.CapturedShift;
                        currentAction.UseWin = state.CapturedWin;
                        currentAction.Key = state.CapturedKey;
                        updated = true;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.Separator();
                ImGui.Text("Manual Setup:");

                // Manual setup as fallback
                var useCtrl = currentAction.UseCtrl;
                var useAlt = currentAction.UseAlt;
                var useShift = currentAction.UseShift;
                var useWin = currentAction.UseWin;
                var key = currentAction.Key;

                if (ImGui.Checkbox($"CTRL##manual_ctrl{id}", ref useCtrl))
                {
                    currentAction.UseCtrl = useCtrl;
                    updated = true;
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"ALT##manual_alt{id}", ref useAlt))
                {
                    currentAction.UseAlt = useAlt;
                    updated = true;
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"SHIFT##manual_shift{id}", ref useShift))
                {
                    currentAction.UseShift = useShift;
                    updated = true;
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"WIN##manual_win{id}", ref useWin))
                {
                    currentAction.UseWin = useWin;
                    updated = true;
                }

                ImGui.Text("Key:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                if (ImGuiHelper.NonContinuousEnumComboBox($"##manual_key{id}", ref key))
                {
                    currentAction.Key = key;
                    updated = true;
                }

                if (ImGui.Button("Close"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            return updated;
        }

        private static void CheckForKeyCapture(KeyCaptureState state)
        {
            // Check for ESC to cancel
            if (IsKeyPressed(VK.ESCAPE))
            {
                state.IsCapturing = false;
                return;
            }

            // Check for modifier keys
            if (IsKeyPressed(VK.LCONTROL) || IsKeyPressed(VK.RCONTROL))
            {
                state.CapturedCtrl = true;
            }
            if (IsKeyPressed(VK.LMENU) || IsKeyPressed(VK.RMENU))
            {
                state.CapturedAlt = true;
            }
            if (IsKeyPressed(VK.LSHIFT) || IsKeyPressed(VK.RSHIFT))
            {
                state.CapturedShift = true;
            }
            if (IsKeyPressed(VK.LWIN) || IsKeyPressed(VK.RWIN))
            {
                state.CapturedWin = true;
            }

            // Check for main keys (A-Z, 0-9, F1-F12, etc.)
            var keysToCheck = new[]
            {
                // Letters
                VK.KEY_A, VK.KEY_B, VK.KEY_C, VK.KEY_D, VK.KEY_E, VK.KEY_F, VK.KEY_G, VK.KEY_H,
                VK.KEY_I, VK.KEY_J, VK.KEY_K, VK.KEY_L, VK.KEY_M, VK.KEY_N, VK.KEY_O, VK.KEY_P,
                VK.KEY_Q, VK.KEY_R, VK.KEY_S, VK.KEY_T, VK.KEY_U, VK.KEY_V, VK.KEY_W, VK.KEY_X,
                VK.KEY_Y, VK.KEY_Z,
                
                // Numbers
                VK.KEY_0, VK.KEY_1, VK.KEY_2, VK.KEY_3, VK.KEY_4, VK.KEY_5, VK.KEY_6, VK.KEY_7,
                VK.KEY_8, VK.KEY_9,
                
                // Function keys
                VK.F1, VK.F2, VK.F3, VK.F4, VK.F5, VK.F6, VK.F7, VK.F8, VK.F9, VK.F10, VK.F11, VK.F12,
                
                // Other common keys
                VK.SPACE, VK.RETURN, VK.TAB, VK.BACK, VK.DELETE, VK.INSERT, VK.HOME, VK.END,
                VK.PRIOR, VK.NEXT, VK.UP, VK.DOWN, VK.LEFT, VK.RIGHT,
                
                // Mouse buttons
                VK.LBUTTON, VK.RBUTTON, VK.MBUTTON
            };

            foreach (var key in keysToCheck)
            {
                if (IsKeyPressed(key))
                {
                    state.CapturedKey = key;
                    state.HasCapturedKey = true;
                    break;
                }
            }
        }

        private static bool IsKeyPressed(VK key)
        {
            // Simple key press detection using Windows API
            try
            {
                return (GetAsyncKeyState((int)key) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}