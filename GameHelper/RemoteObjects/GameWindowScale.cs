// <copyright file="GameWindowScale.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Initially this class reads the game window scale value from the game.
    ///     But that was PITA since it's memory location changed a lot. So, this
    ///     class was updated to just calculate the game window scale value in the
    ///     same way the game does it.
    /// </summary>
    public class GameWindowScale : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GameWindowScale" /> class.
        /// </summary>
        internal GameWindowScale()
            : base(IntPtr.Zero)
        {
            CoroutineHandler.Start(this.OnGameMove(), priority: int.MaxValue - 1);
            CoroutineHandler.Start(this.OnGameForegroundChange(), priority: int.MaxValue - 1);
        }

        /// <summary>
        ///     Gets the current game window scale values.
        ///     There are 3 rows (1 for each UI Element type)
        ///     and each row contains 2 element.
        /// </summary>
        public float[] Values { get; } = new float[6];

        /// <summary>
        ///     Gets the Scale Value depending on the index and multiplier.
        /// </summary>
        /// <param name="index">index read from the Ui-Element.</param>
        /// <param name="multiplier">multiplier read from the Ui-Element.</param>
        /// <returns>Returns the Width and Height scale value valid for the specific Ui-Element.</returns>
        public (float WidthScale, float HeightScale) GetScaleValue(int index, float multiplier)
        {
            var widthScale = multiplier;
            var heightScale = multiplier;
            switch (index)
            {
                case 1:
                    widthScale *= this.Values[0];
                    heightScale *= this.Values[1];
                    break;
                case 2:
                    widthScale *= this.Values[2];
                    heightScale *= this.Values[3];
                    break;
                case 3:
                    widthScale *= this.Values[4];
                    heightScale *= this.Values[5];
                    break;
            }

            return (widthScale, heightScale);
        }

        // Rendering is handled by a provider.

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            for (var i = 0; i < this.Values.Length; i++)
            {
                this.Values[i] = 1f;
            }
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            // All of the code below is written after RE-ing the game function.
            var v1 = (float)((Core.Process.WindowArea.Width - Core.GameCull.Value - Core.GameCull.Value) / UiElementBaseFuncs.BaseResolution.X);
            var v2 = (float)((Core.Process.WindowArea.Height - 0) / UiElementBaseFuncs.BaseResolution.Y);
            this.Values[0] = v1;
            this.Values[1] = v1;
            this.Values[2] = v2;
            this.Values[3] = v2;
            this.Values[4] = v1;
            this.Values[5] = v2;
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