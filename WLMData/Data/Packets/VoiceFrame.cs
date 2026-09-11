using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;
using System.IO;
using NetworkCommsDotNet.DPSBase;

namespace WLMData.Data.Packets
{
    /// <summary>
    /// A short slice of call audio, mu-law encoded. On the way to the server <see cref="id"/> is
    /// the other party; on the way back out it is whoever spoke.
    /// </summary>
    [ProtoContract]
    public class VoiceFrame : Serializer, IExplicitlySerialize
    {
        /// <summary>A frame far larger than 40 ms of audio is not one of ours.</summary>
        public const int MaximumSize = 4096;

        [ProtoMember(1)]
        public string id { get; private set; }

        [ProtoMember(2)]
        public byte[] data { get; private set; }

        private VoiceFrame() { }

        public VoiceFrame(string id, byte[] data)
        {
            this.id = id;
            this.data = data;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, id);
        }

        public void Deserialize(Stream inputStream)
        {
            id = DeserializeString(inputStream);
        }

        public static void Deserialize(Stream inputStream, out VoiceFrame frame)
        {
            frame = new VoiceFrame();
            frame.Deserialize(inputStream);
        }
    }
}
