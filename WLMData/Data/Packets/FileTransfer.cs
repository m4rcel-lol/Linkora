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
    /// A file sent inside a conversation. Travels over the same encrypted connection as chat
    /// messages: on the way to the server <see cref="id"/> is the recipient, on the way back out it
    /// is the sender.
    /// </summary>
    [ProtoContract]
    public class FileTransfer : Serializer, IExplicitlySerialize
    {
        /// <summary>Largest attachment accepted, in bytes.</summary>
        public const int MaximumSize = 20 * 1024 * 1024;

        [ProtoMember(1)]
        public string id { get; private set; }

        [ProtoMember(2)]
        public string fileName { get; private set; }

        [ProtoMember(3)]
        public byte[] data { get; private set; }

        private FileTransfer() { }

        public FileTransfer(string id, string fileName, byte[] data)
        {
            this.id = id;
            this.fileName = fileName;
            this.data = data;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, id);
            SerializeData(outputStream, fileName);
        }

        public void Deserialize(Stream inputStream)
        {
            id = DeserializeString(inputStream);
            fileName = DeserializeString(inputStream);
        }

        public static void Deserialize(Stream inputStream, out FileTransfer fileTransfer)
        {
            fileTransfer = new FileTransfer();
            fileTransfer.Deserialize(inputStream);
        }
    }
}
