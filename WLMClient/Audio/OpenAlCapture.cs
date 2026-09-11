using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace WLMClient.Audio
{
    /// <summary>
    /// Records from the default microphone through OpenAL's capture extension.
    ///
    /// Silk.NET does not bind these five entry points, but the OpenAL Soft library it ships does
    /// export them, so they are called directly here.
    /// </summary>
    class OpenAlCapture : IDisposable
    {
        private const string Library = "openal";

        /// <summary>16 bit mono, matching what the codec and playback expect.</summary>
        private const int FormatMono16 = 0x1101;

        private const int CaptureSamplesAvailable = 0x312;

        private IntPtr device;
        private Thread worker;
        private volatile bool running;

        /// <summary>Raised for each frame of samples read from the microphone.</summary>
        public event Action<short[]> FrameCaptured;

        static OpenAlCapture()
        {
            NativeAudio.EnsureResolver();
        }

        /// <summary>Opens the microphone and starts reading. Returns false if there is none.</summary>
        public bool Start()
        {
            try
            {
                // A generous buffer, so a late poll loses nothing.
                device = alcCaptureOpenDevice(null, VoiceCodec.SampleRate, FormatMono16,
                    VoiceCodec.FrameSamples * 16);

                if (device == IntPtr.Zero)
                {
                    return false;
                }

                alcCaptureStart(device);

                running = true;

                worker = new Thread(Run);
                worker.IsBackground = true;
                worker.Name = "Voice capture";
                worker.Start();

                return true;
            }
            catch
            {
                // No capture support on this machine; the call continues without a microphone.
                return false;
            }
        }

        private void Run()
        {
            short[] frame = new short[VoiceCodec.FrameSamples];

            while (running)
            {
                int available = 0;

                try
                {
                    alcGetIntegerv(device, CaptureSamplesAvailable, 1, ref available);
                }
                catch
                {
                    return;
                }

                if (available < VoiceCodec.FrameSamples)
                {
                    Thread.Sleep(5);

                    continue;
                }

                GCHandle handle = GCHandle.Alloc(frame, GCHandleType.Pinned);

                try
                {
                    alcCaptureSamples(device, handle.AddrOfPinnedObject(), VoiceCodec.FrameSamples);
                }
                finally
                {
                    handle.Free();
                }

                Action<short[]> handler = FrameCaptured;

                if (handler != null)
                {
                    handler(frame);
                }
            }
        }

        public void Dispose()
        {
            running = false;

            try
            {
                if (worker != null)
                {
                    worker.Join(500);
                }
            }
            catch
            {
            }

            if (device != IntPtr.Zero)
            {
                try
                {
                    alcCaptureStop(device);
                    alcCaptureCloseDevice(device);
                }
                catch
                {
                }

                device = IntPtr.Zero;
            }
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr alcCaptureOpenDevice(string deviceName, uint frequency, int format, int bufferSize);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool alcCaptureCloseDevice(IntPtr device);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void alcCaptureStart(IntPtr device);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void alcCaptureStop(IntPtr device);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void alcCaptureSamples(IntPtr device, IntPtr buffer, int samples);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void alcGetIntegerv(IntPtr device, int param, int size, ref int data);
    }

    /// <summary>Points "openal" at the library name each platform actually ships.</summary>
    static class NativeAudio
    {
        private static bool installed;

        public static void EnsureResolver()
        {
            if (installed)
            {
                return;
            }

            installed = true;

            NativeLibrary.SetDllImportResolver(typeof(NativeAudio).Assembly, (name, assembly, path) =>
            {
                if (!string.Equals(name, "openal", StringComparison.OrdinalIgnoreCase))
                {
                    return IntPtr.Zero;
                }

                string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { "soft_oal.dll", "openal32.dll" }
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? new[] { "libopenal.dylib", "libopenal.1.dylib" }
                        : new[] { "libopenal.so", "libopenal.so.1" };

                foreach (string candidate in candidates)
                {
                    IntPtr handle;

                    if (NativeLibrary.TryLoad(candidate, assembly, path, out handle))
                    {
                        return handle;
                    }
                }

                return IntPtr.Zero;
            });
        }
    }
}
