using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Threading;

using WLMClient.Compat;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMClient.UI.Windows;
using WLMData.Enums;
using WLMData.Data.Packets;
using WLMClient.Locale;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Handles an attachment sent by a contact.</summary>
    class ReceiveFile : PacketHandler
    {
        public ReceiveFile(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<FileTransfer>(PacketName.sendFileTransfer.ToString(), IncomingFile);
        }

        protected void IncomingFile(PacketHeader header, Connection connection, FileTransfer fileTransfer)
        {
            PostLoginAction(fileTransfer);
        }

        public override void PostLoginAction(object packet)
        {
            base.PostLoginAction(packet);

            mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                ManageChatWindows.ReceiveFile((FileTransfer)packet);
            }));
        }
    }
}
