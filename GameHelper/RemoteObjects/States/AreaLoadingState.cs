// <copyright file="AreaLoadingState.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.States
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects.States;

    /// <summary>
    ///     Reads AreaLoadingState Game Object.
    /// </summary>
    public sealed class AreaLoadingState : RemoteObjectBase
    {
        private AreaLoadingStateOffset lastCache;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AreaLoadingState" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal AreaLoadingState(IntPtr address)
            : base(address)
        {
            CoroutineHandler.Start(this.OnPerFrame(), priority: int.MaxValue - 1);
        }

        /// <summary>
        ///     Gets the game current Area Name.
        /// </summary>
        public string CurrentAreaName { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets a value indicating whether the game is in loading screen or not.
        /// </summary>
        internal bool IsLoading { get; private set; }

        // Rendering is handled by a provider.

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.lastCache = default;
            this.CurrentAreaName = string.Empty;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<AreaLoadingStateOffset>(this.Address);
            this.IsLoading = data.IsLoading == 0x01;
            var hasAreaChanged = false;
            if (data.CurrentAreaDetailsPtr != IntPtr.Zero &&
                !this.IsLoading &&
                data.TotalLoadingScreenTimeMs > this.lastCache.TotalLoadingScreenTimeMs)
            {
                var areaName = reader.ReadUnicodeString(reader.ReadMemory<IntPtr>(data.CurrentAreaDetailsPtr));
                this.CurrentAreaName = areaName;
                this.lastCache = data;
                hasAreaChanged = true;
            }

            if (hasAreaChanged)
            {
                CoroutineHandler.InvokeLater(new Wait(0.1d), () => { CoroutineHandler.RaiseEvent(RemoteEvents.AreaChanged); });
            }
        }

        private IEnumerator<Wait> OnPerFrame()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.PerFrameDataUpdate);
                if (this.Address != IntPtr.Zero)
                {
                    this.UpdateData(false);
                }
            }
        }
    }
}