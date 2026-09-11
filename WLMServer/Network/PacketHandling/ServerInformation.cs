using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMData.Enums;
using WLMData.Data.Packets;

namespace WLMServer.Network.PacketHandling
{
    /// <summary>
    /// Answers the sign in page's question about where to sign up. Deliberately the one thing this
    /// server will say to somebody who has not signed in, and it says nothing about any account.
    /// </summary>
    class ServerInformation : PacketHandler
    {
        public ServerInformation(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<string>(
                PacketName.requestServerInfo.ToString(), Request);
        }

        protected void Request(PacketHeader header, Connection connection, string unused)
        {
            server.SendPacket(connection, PacketName.sendServerInfo.ToString(),
                new ServerInfo(WebServer.GetRegistrationUrl(), Config.Properties.SERVER_NAME));
        }
    }
}
