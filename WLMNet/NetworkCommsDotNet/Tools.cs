using System.Collections.Generic;
using NetworkCommsDotNet.DPSBase;

namespace NetworkCommsDotNet.Tools
{
    /// <summary>
    /// Pre shared key encryption for packet payloads. The password is stored in the send/receive
    /// options by <see cref="AddPasswordToOptions"/> and picked up when a frame is written or read.
    /// </summary>
    public sealed class RijndaelPSKEncrypter : DataProcessor
    {
        /// <summary>Key under which the pre shared key lives inside the options dictionary.</summary>
        public const string PasswordOption = "RijndaelPSKEncrypter_PASSWORD";

        public static void AddPasswordToOptions(Dictionary<string, string> options, string password)
        {
            options[PasswordOption] = password;
        }

        internal static string GetPassword(Dictionary<string, string> options)
        {
            string password;
            if (options != null && options.TryGetValue(PasswordOption, out password))
            {
                return password;
            }

            return null;
        }

        public override byte[] ForwardProcessDataStream(byte[] input, Dictionary<string, string> options)
        {
            string password = GetPassword(options);
            if (password == null)
            {
                return input;
            }

            return Wire.Encrypt(input, Wire.DeriveKey(password));
        }

        public override byte[] ReverseProcessDataStream(byte[] input, Dictionary<string, string> options)
        {
            string password = GetPassword(options);
            if (password == null)
            {
                return input;
            }

            return Wire.Decrypt(input, Wire.DeriveKey(password));
        }
    }
}
