using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using WLMServer.Network;

using WLMData.Enums;
using WLMData.Data.Packets;
using WLMServer.Network.UserData;

namespace WLMServer.Network.PacketHandling
{
    /// <summary>
    /// Relays a chat attachment to its recipient. The file is passed straight through rather than
    /// stored, so it never touches the server's disk.
    /// </summary>
    class TransferFile : PacketHandler
    {
        public TransferFile(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<FileTransfer>(PacketName.sendFileTransfer.ToString(), Transfer);
        }

        protected void Transfer(PacketHeader header, Connection connection, FileTransfer fileTransfer)
        {
            if (fileTransfer == null || fileTransfer.data == null)
            {
                return;
            }

            if (fileTransfer.data.Length > FileTransfer.MaximumSize)
            {
                Program.WriteToConsole("Rejected an oversized attachment (" + fileTransfer.data.Length + " bytes).");

                return;
            }

            Connection targetUserConnection = server.GetConnectionFromUserID(fileTransfer.id);
            ConnectedUser senderUser = server.GetConnectedUser(connection);

            if (targetUserConnection != null)
            {
                server.SendPacket(targetUserConnection, PacketName.sendFileTransfer.ToString(),
                    new FileTransfer(senderUser.user.id, fileTransfer.fileName, fileTransfer.data));
            }
        }
    }
}
