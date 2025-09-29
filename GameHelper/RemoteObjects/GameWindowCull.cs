// <copyright file="GameWindowCull.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;

    /// <summary>
    ///     Reads the Game Window Cull (black bar on each side) values from the game.
    ///     Only reads when game window moves/resize.
    /// </summary>
    public class GameWindowCull : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GameWindowCull" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal GameWindowCull(IntPtr address)
            : base(address)
        {
            CoroutineHandler.Start(this.OnGameMove(), priority: int.MaxValue);
            CoroutineHandler.Start(this.OnGameForegroundChange(), priority: int.MaxValue);
        }

        /// <summary>
        ///     Gets the current game window scale values.
        /// </summary>
        public int Value { get; private set; }

        // Rendering is handled by a provider.

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.Value = 0;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            this.Value = reader.ReadMemory<int>(this.Address);
        }

        private IEnumerator<Wait> OnGameMove()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnMoved);

                // No need to check for IntPtr.zero
                // because game will only move when it exists. :D
                this.UpdateData(false);
            }
        }

        private IEnumerator<Wait> OnGameForegroundChange()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnForegroundChanged);

                // No need to check for IntPtr.zero
                // because game will only move when it exists. :D
                this.UpdateData(false);
            }
        }
    }
}