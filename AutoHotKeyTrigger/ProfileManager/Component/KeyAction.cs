// <copyright file="KeyAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutoHotKeyTrigger.ProfileManager.Component
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Utils;
    using Newtonsoft.Json;

    /// <summary>
    ///     Represents a single key action with an optional delay before execution
    /// </summary>
    public class KeyAction
    {
        // Windows API constants for SendInput
        private const int INPUT_KEYBOARD = 1;
        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        /// <summary>
        ///     The main key to press
        /// </summary>
        [JsonProperty]
        public VK Key { get; set; }

        /// <summary>
        ///     Delay in milliseconds before pressing this key (default 0)
        /// </summary>
        [JsonProperty]
        public int DelayMs { get; set; } = 0;

        /// <summary>
        ///     Whether to hold CTRL modifier
        /// </summary>
        [JsonProperty]
        public bool UseCtrl { get; set; } = false;

        /// <summary>
        ///     Whether to hold ALT modifier
        /// </summary>
        [JsonProperty]
        public bool UseAlt { get; set; } = false;

        /// <summary>
        ///     Whether to hold SHIFT modifier
        /// </summary>
        [JsonProperty]
        public bool UseShift { get; set; } = false;

        /// <summary>
        ///     Whether to hold WINDOWS modifier
        /// </summary>
        [JsonProperty]
        public bool UseWin { get; set; } = false;

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        public KeyAction()
        {
            Key = VK.KEY_1;
            DelayMs = 0;
            UseCtrl = false;
            UseAlt = false;
            UseShift = false;
            UseWin = false;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        /// <param name="key">The key to press</param>
        /// <param name="delayMs">Delay in milliseconds before pressing this key</param>
        /// <param name="useCtrl">Whether to hold CTRL</param>
        /// <param name="useAlt">Whether to hold ALT</param>
        /// <param name="useShift">Whether to hold SHIFT</param>
        /// <param name="useWin">Whether to hold WIN</param>
        public KeyAction(VK key, int delayMs = 0, bool useCtrl = false, bool useAlt = false, bool useShift = false, bool useWin = false)
        {
            Key = key;
            DelayMs = delayMs;
            UseCtrl = useCtrl;
            UseAlt = useAlt;
            UseShift = useShift;
            UseWin = useWin;
        }

        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="other">KeyAction to copy from</param>
        public KeyAction(KeyAction other)
        {
            Key = other.Key;
            DelayMs = other.DelayMs;
            UseCtrl = other.UseCtrl;
            UseAlt = other.UseAlt;
            UseShift = other.UseShift;
            UseWin = other.UseWin;
        }

        /// <summary>
        ///     Gets a display string representation of this key action
        /// </summary>
        /// <returns>String like "CTRL+X" or just "F" for single keys</returns>
        public string GetDisplayString()
        {
            var modifiers = new List<string>();
            if (UseCtrl) modifiers.Add("CTRL");
            if (UseAlt) modifiers.Add("ALT");
            if (UseShift) modifiers.Add("SHIFT");
            if (UseWin) modifiers.Add("WIN");

            var keyStr = Key.ToString().Replace("KEY_", "");
            
            if (modifiers.Count > 0)
            {
                return string.Join("+", modifiers) + "+" + keyStr;
            }
            
            return keyStr;
        }

        /// <summary>
        ///     Executes this key action (presses the key with modifiers)
        /// </summary>
        /// <returns>True if successful</returns>
        public bool Execute()
        {
            try
            {
                // If no modifiers, use the simple approach
                if (!UseCtrl && !UseAlt && !UseShift && !UseWin)
                {
                    return MiscHelper.KeyUp(Key);
                }

                // Use SendInput for proper modifier key combinations
                var inputs = new List<INPUT>();

                // Press modifier keys down first
                if (UseCtrl) inputs.Add(CreateKeyInput(VK.LCONTROL, KEYEVENTF_KEYDOWN));
                if (UseAlt) inputs.Add(CreateKeyInput(VK.LMENU, KEYEVENTF_KEYDOWN));
                if (UseShift) inputs.Add(CreateKeyInput(VK.LSHIFT, KEYEVENTF_KEYDOWN));
                if (UseWin) inputs.Add(CreateKeyInput(VK.LWIN, KEYEVENTF_KEYDOWN));

                // Press and release the main key
                inputs.Add(CreateKeyInput(Key, KEYEVENTF_KEYDOWN));
                inputs.Add(CreateKeyInput(Key, KEYEVENTF_KEYUP));

                // Release modifier keys in reverse order
                if (UseWin) inputs.Add(CreateKeyInput(VK.LWIN, KEYEVENTF_KEYUP));
                if (UseShift) inputs.Add(CreateKeyInput(VK.LSHIFT, KEYEVENTF_KEYUP));
                if (UseAlt) inputs.Add(CreateKeyInput(VK.LMENU, KEYEVENTF_KEYUP));
                if (UseCtrl) inputs.Add(CreateKeyInput(VK.LCONTROL, KEYEVENTF_KEYUP));

                // Send all inputs
                uint result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                return result == inputs.Count;
            }
            catch (Exception)
            {
                // Fallback: try to release any stuck modifier keys
                try
                {
                    if (UseCtrl) SendSingleKey(VK.LCONTROL, KEYEVENTF_KEYUP);
                    if (UseAlt) SendSingleKey(VK.LMENU, KEYEVENTF_KEYUP);
                    if (UseShift) SendSingleKey(VK.LSHIFT, KEYEVENTF_KEYUP);
                    if (UseWin) SendSingleKey(VK.LWIN, KEYEVENTF_KEYUP);
                }
                catch { }
                
                return false;
            }
        }

        private static INPUT CreateKeyInput(VK key, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private static void SendSingleKey(VK key, uint flags)
        {
            var input = new INPUT[]
            {
                CreateKeyInput(key, flags)
            };
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}