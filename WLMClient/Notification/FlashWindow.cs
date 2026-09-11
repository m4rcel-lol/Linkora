using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Avalonia.Controls;

using WLMClient.Compat;

/// <summary>
/// Asks the desktop to draw the user's attention to a window. The Win32 FlashWindowEx call is
/// replaced by the per platform equivalent: a dock bounce on macOS, a taskbar flash on Windows.
/// </summary>
public static class FlashWindowManager
{
    public static void FlashWindow(this Window win, UInt32 count = UInt32.MaxValue)
    {
        // Don't flash if the window is active
        if (win == null || win.IsActive)
        {
            return;
        }

        Platform.RequestAttention(win);
    }

    public static void StopFlashingWindow(this Window win)
    {
        // The platforms targeted here stop asking for attention as soon as the window is focused,
        // so there is nothing to cancel.
    }
}
