using System;

using Avalonia.Controls;

namespace WLMClient.Compat
{
    /// <summary>
    /// A masked text field exposing the WPF PasswordBox surface (a <c>Password</c> property) on top
    /// of Avalonia's TextBox.
    /// </summary>
    public class PasswordBox : TextBox
    {
        public PasswordBox()
        {
            PasswordChar = '●';
        }

        /// <summary>
        /// Avalonia looks control themes up by the exact runtime type, so without this a derived
        /// text box would get no template at all and render as nothing.
        /// </summary>
        protected override Type StyleKeyOverride
        {
            get { return typeof(TextBox); }
        }

        public string Password
        {
            get { return Text ?? string.Empty; }
            set { Text = value; }
        }

        public new void Clear()
        {
            Text = string.Empty;
        }
    }
}
