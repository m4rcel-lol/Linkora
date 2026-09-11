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
using WLMServer.Database;
using WLMServer.Network.UserData;

namespace WLMServer.Network.PacketHandling
{
    /// <summary>
    /// Changes the name an account signs in with. Everything that refers to the old name has to
    /// move with it, so the database work is done in one transaction and the contact lists held in
    /// memory for connected users are corrected afterwards.
    /// </summary>
    class ChangeUsername : PacketHandler
    {
        public ChangeUsername(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<string>(
                PacketName.requestUsernameChange.ToString(), Change);
        }

        protected void Change(PacketHeader header, Connection connection, string requestedUsername)
        {
            ConnectedUser senderUser = server.GetConnectedUser(connection);

            if (senderUser == null)
            {
                return;
            }

            string oldId = senderUser.user.id;
            string newId = (requestedUsername ?? "").Trim();

            if (!AccountManager.IsValidUsername(newId) ||
                string.Equals(newId, oldId, StringComparison.OrdinalIgnoreCase))
            {
                Reply(connection, UsernameChangeResultCode.invalid, oldId);

                return;
            }

            if (server.accountManager.IsUserInDatabase(newId))
            {
                Reply(connection, UsernameChangeResultCode.taken, oldId);

                return;
            }

            if (!server.accountManager.RenameAccount(oldId, newId))
            {
                Reply(connection, UsernameChangeResultCode.failed, oldId);

                return;
            }

            server.RenameUser(connection, oldId, newId);

            Program.WriteToConsole("User '" + oldId + "' is now known as '" + newId + "'.");

            Reply(connection, UsernameChangeResultCode.success, newId);

            // Let the user and their contacts see the new name straight away.
            server.SendPersonalUserUpdate(connection, server.GetConnectedUser(connection).user);
            server.SendUsersContactList(connection, newId);
            server.SendUpdateToUsersContactList(connection, server.GetConnectedUser(connection).user);
        }

        private void Reply(Connection connection, UsernameChangeResultCode code, string username)
        {
            server.SendPacket(connection, PacketName.sendUsernameChangeResult.ToString(),
                new UsernameChangeResult((int)code, username));
        }
    }
}
