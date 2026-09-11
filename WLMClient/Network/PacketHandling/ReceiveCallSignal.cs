using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Threading;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMClient.UI.Windows;
using WLMData.Enums;
using WLMData.Data.Packets;
using WLMClient.Locale;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Handles call setup and teardown coming from a contact.</summary>
    class ReceiveCallSignal : PacketHandler
    {
        public ReceiveCallSignal(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<CallSignal>(
                PacketName.sendCallSignal.ToString(), Signal);
        }

        protected void Signal(PacketHeader header, Connection connection, CallSignal callSignal)
        {
            PostLoginAction(callSignal);
        }

        public override void PostLoginAction(object packet)
        {
            base.PostLoginAction(packet);

            CallManager.Handle((CallSignal)packet);
        }
    }
}
