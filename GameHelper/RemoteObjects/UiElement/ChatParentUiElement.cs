namespace GameHelper.RemoteObjects.UiElement
{
    using GameHelper.Cache;
    using System;

    /// <summary>
    ///     Points to the Chatbox parent UiElement object.
    /// </summary>
    public class ChatParentUiElement : UiElementBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ChatParentUiElement" /> class.
        /// </summary>
        /// <param name="address">address to the Chat Parent Ui Element of the game.</param>
        /// <param name="parents">parent cache to use for this Ui Element.</param>
        internal ChatParentUiElement(IntPtr address, UiElementParents parents) :
            base(address, parents) {}

        public bool IsChatActive => this.backgroundColor.W * 255 >= 0x8C;

        // Rendering is handled by a provider.
    }
}
