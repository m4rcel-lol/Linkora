using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace WLMClient.Compat
{
    /// <summary>
    /// Lets the ported code keep WPF's <c>Dispatcher.Invoke(priority, action)</c> argument order.
    /// </summary>
    public static class DispatcherExtensions
    {
        public static void Invoke(this Dispatcher dispatcher, DispatcherPriority priority, Action action)
        {
            dispatcher.Invoke(action, priority);
        }

        public static void BeginInvoke(this Dispatcher dispatcher, DispatcherPriority priority, Action action)
        {
            dispatcher.Post(action, priority);
        }
    }

    /// <summary>Stand in for the pieces of WPF's Keyboard class the client used.</summary>
    public static class Keyboard
    {
        /// <summary>Moves focus off whatever currently holds it.</summary>
        public static void ClearFocus(Control context)
        {
            if (context == null)
            {
                return;
            }

            TopLevel topLevel = TopLevel.GetTopLevel(context);

            if (topLevel != null && topLevel.FocusManager != null)
            {
                topLevel.FocusManager.ClearFocus();
            }
        }
    }
}
