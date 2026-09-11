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
using WLMClient.UI.Controls.Dialogs;

namespace WLMClient.Network.PacketHandling
{
    class ReceiveWritingStatus : PacketHandler
    {
        public ReceiveWritingStatus(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<WritingStatus>(PacketName.sendWritingStatus.ToString(), Status);
        }

        protected void Status(PacketHeader header, Connection connection, WritingStatus writingStatus)
        {
            PostLoginAction(writingStatus);
        }

        public override void PostLoginAction(object packet)
        {
            base.PostLoginAction(packet);

            mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                ManageChatWindows.ReceiveWritingStatus((WritingStatus)packet);
            }));
        }
    }
}