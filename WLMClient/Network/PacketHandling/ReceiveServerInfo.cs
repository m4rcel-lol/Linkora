using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMClient.UI.Windows;
using WLMData.Enums;
using WLMData.Data.Packets;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Hands the server's answer to whoever asked for it.</summary>
    class ReceiveServerInfo : PacketHandler
    {
        public ReceiveServerInfo(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<ServerInfo>(
                PacketName.sendServerInfo.ToString(), Info);
        }

        protected void Info(PacketHeader header, Connection connection, ServerInfo info)
        {
            Client.CompleteServerInfo(info);
        }
    }
}
