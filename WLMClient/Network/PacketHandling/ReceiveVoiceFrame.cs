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
using WLMClient.Locale;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Feeds call audio arriving from the other side straight into playback.</summary>
    class ReceiveVoiceFrame : PacketHandler
    {
        public ReceiveVoiceFrame(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<VoiceFrame>(
                PacketName.sendVoiceFrame.ToString(), Frame);
        }

        protected void Frame(PacketHeader header, Connection connection, VoiceFrame frame)
        {
            // Straight to the audio device off the receive thread; going through the interface
            // thread would add delay for no reason.
            CallManager.ReceiveVoice(frame);
        }
    }
}
