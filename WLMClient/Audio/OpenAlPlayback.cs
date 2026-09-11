using System;
using System.Collections.Generic;

using Silk.NET.OpenAL;

namespace WLMClient.Audio
{
    /// <summary>
    /// Plays the far end of a call through OpenAL, queueing each frame onto a streaming source and
    /// recycling the buffers the device has finished with.
    /// </summary>
    unsafe class OpenAlPlayback : IDisposable
    {
        private AL al;
        private ALContext alc;

        private Device* device;
        private Context* context;

        private uint source;
        private readonly Queue<uint> spare = new Queue<uint>();
        private readonly object locker = new object();

        private bool started;

        /// <summary>Opens the default output. Returns false if there is no usable device.</summary>
        public bool Start()
        {
            try
            {
                alc = ALContext.GetApi();
                al = AL.GetApi();

                device = alc.OpenDevice("");

                if (device == null)
                {
                    return false;
                }

                context = alc.CreateContext(device, null);
                alc.MakeContextCurrent(context);

                source = al.GenSource();

                started = true;

                return true;
            }
            catch
            {
                // No output device; the call continues without hearing the other side.
                return false;
            }
        }

        /// <summary>Queues one frame of samples for playback.</summary>
        public void Play(short[] samples)
        {
            if (!started || samples == null || samples.Length == 0)
            {
                return;
            }

            lock (locker)
            {
                try
                {
                    Recycle();

                    uint buffer = spare.Count > 0 ? spare.Dequeue() : al.GenBuffer();

                    fixed (short* data = samples)
                    {
                        al.BufferData(buffer, BufferFormat.Mono16, data,
                            samples.Length * sizeof(short), VoiceCodec.SampleRate);
                    }

                    al.SourceQueueBuffers(source, 1, &buffer);

                    // Starts on the first frame, and again after a gap in the audio drained it.
                    int state;
                    al.GetSourceProperty(source, GetSourceInteger.SourceState, out state);

                    if (state != (int)SourceState.Playing)
                    {
                        al.SourcePlay(source);
                    }
                }
                catch
                {
                    // A dropped frame is better than taking the call down.
                }
            }
        }

        /// <summary>Takes back the buffers the device has finished with, so they can be refilled.</summary>
        private void Recycle()
        {
            int processed;
            al.GetSourceProperty(source, GetSourceInteger.BuffersProcessed, out processed);

            while (processed-- > 0)
            {
                uint buffer;
                al.SourceUnqueueBuffers(source, 1, &buffer);

                spare.Enqueue(buffer);
            }
        }

        public void Dispose()
        {
            lock (locker)
            {
                if (!started)
                {
                    return;
                }

                started = false;

                try
                {
                    al.SourceStop(source);
                    Recycle();

                    while (spare.Count > 0)
                    {
                        uint buffer = spare.Dequeue();
                        al.DeleteBuffer(buffer);
                    }

                    al.DeleteSource(source);

                    alc.MakeContextCurrent(null);
                    alc.DestroyContext(context);
                    alc.CloseDevice(device);
                }
                catch
                {
                    // Shutting down; nothing useful to do about a failure here.
                }
            }
        }
    }
}
