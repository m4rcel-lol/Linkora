using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WLMClient.Locale
{
    /// <summary>
    /// Where received chat attachments are stored, and how to recognise the ones that can be shown
    /// inline in the conversation.
    /// </summary>
    static class Attachments
    {
        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".gif", ".bmp", ".webp" };

        /// <summary>The folder received files are written to, created on first use.</summary>
        public static string DownloadDirectory
        {
            get
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                string root = Directory.Exists(downloads)
                    ? downloads
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                string directory = Path.Combine(root, "Linkora");

                Directory.CreateDirectory(directory);

                return directory;
            }
        }

        public static bool IsImage(string fileName)
        {
            string extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();

            return Array.IndexOf(ImageExtensions, extension) >= 0;
        }

        /// <summary>
        /// Writes an attachment to the download folder. The sender's file name is stripped of any
        /// path information, and an existing file is never overwritten.
        /// </summary>
        public static string Save(string fileName, byte[] data)
        {
            string safeName = Path.GetFileName(fileName ?? "");

            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "attachment";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalid, '_');
            }

            string directory = DownloadDirectory;
            string path = Path.Combine(directory, safeName);

            string baseName = Path.GetFileNameWithoutExtension(safeName);
            string extension = Path.GetExtension(safeName);
            int counter = 1;

            while (File.Exists(path))
            {
                path = Path.Combine(directory, baseName + " (" + counter + ")" + extension);
                counter++;
            }

            File.WriteAllBytes(path, data);

            return path;
        }

        /// <summary>Opens a saved attachment with whatever the desktop uses for that file type.</summary>
        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                System.Diagnostics.ProcessStartInfo startInfo;

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    startInfo = new System.Diagnostics.ProcessStartInfo("open", "\"" + path + "\"");
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    startInfo = new System.Diagnostics.ProcessStartInfo("xdg-open", "\"" + path + "\"");
                }
                else
                {
                    startInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
                }

                startInfo.CreateNoWindow = true;

                System.Diagnostics.Process.Start(startInfo);
            }
            catch
            {
                // Nothing sensible to show the user if the desktop has no handler.
            }
        }

        /// <summary>A human readable size, as shown next to an attachment in the conversation.</summary>
        public static string DescribeSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0.#") + " KB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0.#") + " MB";
        }
    }
}
