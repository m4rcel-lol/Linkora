using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace WLMClient.Compat
{
    /// <summary>
    /// Development aid: when WLM_SCREENSHOT is set to an output directory, every open window is
    /// rendered to a PNG after a short delay. Used to check the ported layout without a desktop
    /// session in the way. Does nothing unless the variable is set.
    /// </summary>
    public static class ScreenshotHelper
    {
        public static void InstallIfRequested()
        {
            string directory = Environment.GetEnvironmentVariable("WLM_SCREENSHOT");

            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            int delayMs = 4000;
            string delayText = Environment.GetEnvironmentVariable("WLM_SCREENSHOT_DELAY");

            if (!string.IsNullOrEmpty(delayText))
            {
                int.TryParse(delayText, out delayMs);
            }

            if (Environment.GetEnvironmentVariable("WLM_SHOW_DIALOGS") == "1")
            {
                DispatcherTimer.RunOnce(ShowDialogsForCapture, TimeSpan.FromMilliseconds(delayMs - 800));
            }

            DispatcherTimer.RunOnce(() => CaptureAll(directory), TimeSpan.FromMilliseconds(delayMs));
        }

        /// <summary>Opens the converted dialogs so their layout can be captured.</summary>
        private static void ShowDialogsForCapture()
        {
            try
            {
                new UI.Controls.Dialogs.FrmAddNewFriend().Show();
                new UI.Controls.Dialogs.FrmOptions().Show();
                new UI.Controls.Dialogs.FrmFriendRequest(
                    new WLMData.Data.Packets.UserInfo("carol", "Carol", "", 3, "", false)).Show();
            }
            catch (Exception exception)
            {
                Console.WriteLine("DIALOG PREVIEW FAILED: " + exception);
            }
        }

        public static void CaptureAll(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);

                IClassicDesktopStyleApplicationLifetime lifetime =
                    Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

                if (lifetime == null)
                {
                    return;
                }

                int index = 0;

                foreach (Window window in lifetime.Windows)
                {
                    Capture(window, Path.Combine(directory,
                        index + "-" + Sanitize(window.GetType().Name) + ".png"));

                    index++;
                }

                Console.WriteLine("SCREENSHOTS WRITTEN: " + index);
            }
            catch (Exception exception)
            {
                Console.WriteLine("SCREENSHOT FAILED: " + exception);
            }
        }

        private static void Capture(Window window, string path)
        {
            PixelSize size = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(window.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(window.Bounds.Height)));

            using (RenderTargetBitmap bitmap = new RenderTargetBitmap(size, new Vector(96, 96)))
            {
                bitmap.Render(window);
                bitmap.Save(path);
            }

            Console.WriteLine("SCREENSHOT: " + path + " (" + size + ")");
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }
}
