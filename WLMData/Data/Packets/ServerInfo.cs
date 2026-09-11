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
    /// What a server will tell anyone who asks, before they have signed in. Answers the one
    /// question the sign in page has: where do people go to get an account here.
    /// </summary>
    [ProtoContract]
    public class ServerInfo : Serializer, IExplicitlySerialize
    {
        /// <summary>
        /// The sign up page. Either a whole URL, or a ":port/path" for the client to put its own
        /// host in front of, because only the client knows which address it reached the server on.
        /// Empty when this server does not offer sign up.
        /// </summary>
        [ProtoMember(1)]
        public string registrationUrl { get; private set; }

        /// <summary>What the server calls itself.</summary>
        [ProtoMember(2)]
        public string name { get; private set; }

        private ServerInfo() { }

        public ServerInfo(string registrationUrl, string name)
        {
            this.registrationUrl = registrationUrl;
            this.name = name;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, registrationUrl);
        }

        public void Deserialize(Stream inputStream)
        {
            registrationUrl = DeserializeString(inputStream);
        }

        public static void Deserialize(Stream inputStream, out ServerInfo info)
        {
            info = new ServerInfo();
            info.Deserialize(inputStream);
        }
    }
}
