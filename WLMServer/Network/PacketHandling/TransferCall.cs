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
    /// Passes call setup and teardown between two users, and keeps track of who is on a call so a
    /// second caller is told the line is busy rather than making the callee's client ring twice.
    /// </summary>
    class TransferCall : PacketHandler
    {
        public TransferCall(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<CallSignal>(
                PacketName.sendCallSignal.ToString(), Transfer);
        }

        protected void Transfer(PacketHeader header, Connection connection, CallSignal signal)
        {
            ConnectedUser senderUser = server.GetConnectedUser(connection);

            if (senderUser == null || signal == null || string.IsNullOrWhiteSpace(signal.id))
            {
                return;
            }

            string from = senderUser.user.id;
            string to = signal.id.Trim();

            Connection targetConnection = server.GetConnectionFromUserID(to);

            switch ((CallSignalType)signal.signal)
            {
                case CallSignalType.invite:
                    if (targetConnection == null)
                    {
                        Reply(connection, to, CallSignalType.unavailable);

                        return;
                    }

                    if (server.IsInCall(to))
                    {
                        Reply(connection, to, CallSignalType.busy);

                        return;
                    }

                    // Held from the invite so a third party cannot ring either of them mid setup.
                    server.BeginCall(from, to);

                    Send(targetConnection, from, CallSignalType.invite);

                    break;

                case CallSignalType.accept:
                    if (targetConnection == null)
                    {
                        server.EndCall(from);
                        Reply(connection, to, CallSignalType.unavailable);

                        return;
                    }

                    Send(targetConnection, from, CallSignalType.accept);

                    break;

                case CallSignalType.decline:
                case CallSignalType.end:
                    server.EndCall(from);

                    if (targetConnection != null)
                    {
                        Send(targetConnection, from, (CallSignalType)signal.signal);
                    }

                    break;
            }
        }

        private void Send(Connection connection, string fromId, CallSignalType type)
        {
            server.SendPacket(connection, PacketName.sendCallSignal.ToString(),
                new CallSignal(fromId, (int)type));
        }

        /// <summary>Tells the caller their invite could not be placed.</summary>
        private void Reply(Connection connection, string aboutId, CallSignalType type)
        {
            server.SendPacket(connection, PacketName.sendCallSignal.ToString(),
                new CallSignal(aboutId, (int)type));
        }
    }
}
