using System;

using WLMData.Data.Packets;

namespace WLMClient.Audio
{
    /// <summary>
    /// The audio side of a call: microphone to the network, network to the speakers. Created when
    /// a call connects and disposed when it ends.
    /// </summary>
    class VoiceSession : IDisposable
    {
        private readonly string contactId;

        private OpenAlCapture capture;
        private OpenAlPlayback playback;

        private bool disposed;

        /// <summary>Whether the microphone is muted. Capture keeps running; nothing is sent.</summary>
        public bool Muted { get; set; }

        /// <summary>Whether a microphone was opened successfully.</summary>
        public bool HasMicrophone { get; private set; }

        /// <summary>Whether an output device was opened successfully.</summary>
        public bool HasSpeakers { get; private set; }

        public VoiceSession(string contactId)
        {
            this.contactId = contactId;
        }

        public void Start()
        {
            playback = new OpenAlPlayback();
            HasSpeakers = playback.Start();

            capture = new OpenAlCapture();
            capture.FrameCaptured += OnFrameCaptured;

            HasMicrophone = capture.Start();
        }

        private void OnFrameCaptured(short[] samples)
        {
            if (disposed || Muted)
            {
                return;
            }

            try
            {
                byte[] encoded = VoiceCodec.Encode(samples, samples.Length);

                Network.Client.SendVoiceFrame(contactId, encoded);
            }
            catch
            {
                // A dropped frame is not worth interrupting the call for.
            }
        }

        /// <summary>Plays a frame that arrived from the other side.</summary>
        public void Receive(byte[] encoded)
        {
            if (disposed || playback == null || encoded == null || encoded.Length == 0)
            {
                return;
            }

            try
            {
                playback.Play(VoiceCodec.Decode(encoded));
            }
            catch
            {
                // Same: skip the frame rather than fail the call.
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (capture != null)
            {
                capture.FrameCaptured -= OnFrameCaptured;
                capture.Dispose();
                capture = null;
            }

            if (playback != null)
            {
                playback.Dispose();
                playback = null;
            }
        }
    }
}
