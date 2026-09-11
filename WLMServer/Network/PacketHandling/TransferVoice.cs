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
    /// Passes call audio between the two people on a call. Frames are only relayed while a call is
    /// actually up, so nobody can stream audio at someone who has not answered.
    /// </summary>
    class TransferVoice : PacketHandler
    {
        public TransferVoice(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<VoiceFrame>(
                PacketName.sendVoiceFrame.ToString(), Transfer);
        }

        protected void Transfer(PacketHeader header, Connection connection, VoiceFrame frame)
        {
            if (frame == null || frame.data == null || frame.data.Length == 0 ||
                frame.data.Length > VoiceFrame.MaximumSize)
            {
                return;
            }

            ConnectedUser senderUser = server.GetConnectedUser(connection);

            if (senderUser == null)
            {
                return;
            }

            string from = senderUser.user.id;

            // Only the person actually on the call with them may be sent audio.
            if (!server.IsCallBetween(from, frame.id))
            {
                return;
            }

            Connection targetConnection = server.GetConnectionFromUserID(frame.id);

            if (targetConnection != null)
            {
                server.SendPacket(targetConnection, PacketName.sendVoiceFrame.ToString(),
                    new VoiceFrame(from, frame.data));
            }
        }
    }
}
