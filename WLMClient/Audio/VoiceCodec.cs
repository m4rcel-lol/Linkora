using System;

namespace WLMClient.Audio
{
    /// <summary>
    /// G.711 mu-law, the companding used by telephony. Halves the bytes on the wire compared with
    /// 16 bit samples and costs nothing to run, which matters more here than the last of the
    /// quality: a call is 16 kHz mono, so this is 128 kbit/s rather than 256.
    /// </summary>
    public static class VoiceCodec
    {
        /// <summary>Samples per second captured and played.</summary>
        public const int SampleRate = 16000;

        /// <summary>Samples in one frame, 40 ms worth.</summary>
        public const int FrameSamples = SampleRate / 25;

        private const int Bias = 0x84;
        private const int Clip = 32635;

        private static readonly short[] DecodeTable = BuildDecodeTable();

        /// <summary>Compresses 16 bit samples to one byte each.</summary>
        public static byte[] Encode(short[] samples, int count)
        {
            byte[] encoded = new byte[count];

            for (int i = 0; i < count; i++)
            {
                encoded[i] = EncodeSample(samples[i]);
            }

            return encoded;
        }

        /// <summary>Expands mu-law bytes back to 16 bit samples.</summary>
        public static short[] Decode(byte[] encoded)
        {
            short[] samples = new short[encoded.Length];

            for (int i = 0; i < encoded.Length; i++)
            {
                samples[i] = DecodeTable[encoded[i]];
            }

            return samples;
        }

        private static byte EncodeSample(short sample)
        {
            // Widened to int before negating: -(-32768) does not fit in a short and would wrap
            // straight back to -32768, which encodes as silence instead of a loud sample.
            int value = sample;
            int sign = (value >> 8) & 0x80;

            if (sign != 0)
            {
                value = -value;
            }

            if (value > Clip)
            {
                value = Clip;
            }

            value += Bias;

            int exponent = 7;

            for (int mask = 0x4000; (value & mask) == 0 && exponent > 0; exponent--, mask >>= 1)
            {
            }

            int mantissa = (value >> (exponent + 3)) & 0x0F;

            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        private static short[] BuildDecodeTable()
        {
            short[] table = new short[256];

            for (int i = 0; i < 256; i++)
            {
                int value = ~i;
                int sign = value & 0x80;
                int exponent = (value >> 4) & 0x07;
                int mantissa = value & 0x0F;

                int sample = ((mantissa << 3) + Bias) << exponent;
                sample -= Bias;

                table[i] = (short)(sign != 0 ? -sample : sample);
            }

            return table;
        }
    }
}
