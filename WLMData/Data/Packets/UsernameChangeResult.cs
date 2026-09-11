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
    /// <summary>The server's answer to a request to change the sign in name.</summary>
    [ProtoContract]
    public class UsernameChangeResult : Serializer, IExplicitlySerialize
    {
        [ProtoMember(1)]
        public int resultCode { get; private set; }

        /// <summary>The name now in use, set when the change succeeded.</summary>
        [ProtoMember(2)]
        public string username { get; private set; }

        private UsernameChangeResult() { }

        public UsernameChangeResult(int resultCode, string username)
        {
            this.resultCode = resultCode;
            this.username = username;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, resultCode);
            SerializeData(outputStream, username);
        }

        public void Deserialize(Stream inputStream)
        {
            resultCode = DeserializeInt(inputStream);
            username = DeserializeString(inputStream);
        }

        public static void Deserialize(Stream inputStream, out UsernameChangeResult result)
        {
            result = new UsernameChangeResult();
            result.Deserialize(inputStream);
        }
    }
}
