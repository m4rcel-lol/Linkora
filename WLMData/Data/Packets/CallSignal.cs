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
    /// One step of a call's setup or teardown. On the way to the server <see cref="id"/> is the
    /// other party being signalled; on the way back out it is whoever sent it.
    /// </summary>
    [ProtoContract]
    public class CallSignal : Serializer, IExplicitlySerialize
    {
        [ProtoMember(1)]
        public string id { get; private set; }

        [ProtoMember(2)]
        public int signal { get; private set; }

        private CallSignal() { }

        public CallSignal(string id, int signal)
        {
            this.id = id;
            this.signal = signal;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, id);
            SerializeData(outputStream, signal);
        }

        public void Deserialize(Stream inputStream)
        {
            id = DeserializeString(inputStream);
            signal = DeserializeInt(inputStream);
        }

        public static void Deserialize(Stream inputStream, out CallSignal callSignal)
        {
            callSignal = new CallSignal();
            callSignal.Deserialize(inputStream);
        }
    }
}
