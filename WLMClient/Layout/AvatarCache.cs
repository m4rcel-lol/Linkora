using System;
using System.Collections.Generic;
using System.Threading;

using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace WLMClient.Layout
{
    /// <summary>
    /// Fetches profile pictures off the UI thread and remembers them, so a contact list with many
    /// avatars neither blocks the interface nor downloads the same picture repeatedly.
    /// </summary>
    static class AvatarCache
    {
        private static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        private static readonly HashSet<string> inFlight = new HashSet<string>();
        private static readonly object locker = new object();

        /// <summary>
        /// Returns the picture if it has already been fetched. Otherwise starts fetching it and
        /// calls <paramref name="onLoaded"/> on the UI thread when it arrives.
        /// </summary>
        public static IImage Get(string url, Action<IImage> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            lock (locker)
            {
                Bitmap existing;

                if (cache.TryGetValue(url, out existing))
                {
                    return existing;
                }

                if (inFlight.Contains(url))
                {
                    // Already being fetched; the request that started it will populate the cache,
                    // and this caller gets its picture on the next refresh.
                    return null;
                }

                inFlight.Add(url);
            }

            Thread worker = new Thread(() => Fetch(url, onLoaded));
            worker.IsBackground = true;
            worker.Name = "Avatar fetch";
            worker.Start();

            return null;
        }

        private static void Fetch(string url, Action<IImage> onLoaded)
        {
            Bitmap bitmap = null;

            try
            {
                bitmap = LoadResource.GetAvatar(url);
            }
            catch
            {
                // A missing or unreachable avatar simply leaves the default picture in place.
            }

            lock (locker)
            {
                inFlight.Remove(url);

                if (bitmap != null)
                {
                    cache[url] = bitmap;
                }
            }

            if (bitmap != null && onLoaded != null)
            {
                Dispatcher.UIThread.Post(() => onLoaded(bitmap));
            }
        }

        /// <summary>Drops a cached picture so a changed avatar is fetched again.</summary>
        public static void Forget(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            lock (locker)
            {
                cache.Remove(url);
            }
        }
    }
}
