// <copyright file="KeyAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutoHotKeyTrigger.ProfileManager.Component
{
    using ClickableTransparentOverlay.Win32;
    using Newtonsoft.Json;

    /// <summary>
    ///     Represents a single key action with an optional delay before execution
    /// </summary>
    public class KeyAction
    {
        /// <summary>
        ///     The key to press
        /// </summary>
        [JsonProperty]
        public VK Key { get; set; }

        /// <summary>
        ///     Delay in milliseconds before pressing this key (default 0)
        /// </summary>
        [JsonProperty]
        public int DelayMs { get; set; } = 0;

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        public KeyAction()
        {
            Key = VK.KEY_1;
            DelayMs = 0;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyAction"/> class.
        /// </summary>
        /// <param name="key">The key to press</param>
        /// <param name="delayMs">Delay in milliseconds before pressing this key</param>
        public KeyAction(VK key, int delayMs = 0)
        {
            Key = key;
            DelayMs = delayMs;
        }

        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="other">KeyAction to copy from</param>
        public KeyAction(KeyAction other)
        {
            Key = other.Key;
            DelayMs = other.DelayMs;
        }
    }
}