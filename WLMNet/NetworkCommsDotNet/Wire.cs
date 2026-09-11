using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NetworkCommsDotNet
{
    /// <summary>
    /// Low level helpers shared by every connection: payload serialisation, the length prefixed
    /// frame format and the pre shared key encryption applied to each frame.
    /// </summary>
    internal static class Wire
    {
        /// <summary>Largest frame we are willing to allocate for, guards against bad input.</summary>
        internal const int MaxFrameSize = 32 * 1024 * 1024;

        internal static class PayloadSerializer
        {
            public static byte[] Serialize(object value)
            {
                if (value == null)
                {
                    return new byte[0];
                }

                if (value is string)
                {
                    return Encoding.UTF8.GetBytes((string)value);
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize(stream, value);
                    return stream.ToArray();
                }
            }

            public static T Deserialize<T>(byte[] bytes)
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)Encoding.UTF8.GetString(bytes);
                }

                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    return ProtoBuf.Serializer.Deserialize<T>(stream);
                }
            }
        }

        /// <summary>Builds the plaintext body of a frame: packet type name followed by the payload.</summary>
        internal static byte[] BuildBody(string packetType, byte[] payload)
        {
            byte[] typeBytes = Encoding.UTF8.GetBytes(packetType ?? string.Empty);
            byte[] body = new byte[4 + typeBytes.Length + 4 + payload.Length];

            int offset = 0;
            WriteInt32(body, ref offset, typeBytes.Length);
            Buffer.BlockCopy(typeBytes, 0, body, offset, typeBytes.Length);
            offset += typeBytes.Length;
            WriteInt32(body, ref offset, payload.Length);
            Buffer.BlockCopy(payload, 0, body, offset, payload.Length);

            return body;
        }

        internal static bool TryParseBody(byte[] body, out string packetType, out byte[] payload)
        {
            packetType = null;
            payload = null;

            if (body.Length < 8)
            {
                return false;
            }

            int offset = 0;
            int typeLength = ReadInt32(body, ref offset);
            if (typeLength < 0 || offset + typeLength + 4 > body.Length)
            {
                return false;
            }

            packetType = Encoding.UTF8.GetString(body, offset, typeLength);
            offset += typeLength;

            int payloadLength = ReadInt32(body, ref offset);
            if (payloadLength < 0 || offset + payloadLength > body.Length)
            {
                return false;
            }

            payload = new byte[payloadLength];
            Buffer.BlockCopy(body, offset, payload, 0, payloadLength);

            return true;
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        private static int ReadInt32(byte[] buffer, ref int offset)
        {
            int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) |
                (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }

        internal static byte[] Int32ToBytes(int value)
        {
            byte[] bytes = new byte[4];
            int offset = 0;
            WriteInt32(bytes, ref offset, value);
            return bytes;
        }

        internal static int BytesToInt32(byte[] bytes)
        {
            int offset = 0;
            return ReadInt32(bytes, ref offset);
        }

        /// <summary>Derives a stable 256 bit key from the configured pre shared key string.</summary>
        internal static byte[] DeriveKey(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));
            }
        }

        /// <summary>AES-CBC encrypts a frame body, prefixing the randomly generated IV.</summary>
        internal static byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
                    byte[] result = new byte[aes.IV.Length + cipher.Length];

                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);

                    return result;
                }
            }
        }

        internal static byte[] Decrypt(byte[] ciphertext, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                if (ciphertext.Length <= 16)
                {
                    throw new CryptographicException("Encrypted frame is too short.");
                }

                byte[] iv = new byte[16];
                Buffer.BlockCopy(ciphertext, 0, iv, 0, 16);

                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(ciphertext, 16, ciphertext.Length - 16);
                }
            }
        }
    }
}
