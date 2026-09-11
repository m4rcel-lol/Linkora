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
    class Authentication : PacketHandler
    {
        public Authentication(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<LoginResult>(PacketName.sendLoginResult.ToString(), Authenticate);
        }

        protected void Authenticate(PacketHeader header, Connection connection, LoginResult loginResult)
        {
            if (loginResult.loginSuccess && loginResult.verifiedVersion)
            {
                Personal.USER_INFO = loginResult.userInfo;
                Config.Properties.AVATAR_IMAGE_UPLOAD_URL = loginResult.avatarUploadAddress;

                mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    mainWindow.LoginSuccess();
                }));
            }
            else
            {
                if (!loginResult.loginSuccess)
                {
                    mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        MessageBox.Show("We can't sign you into Linkora", "Wrong username / password", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }

                if (!loginResult.verifiedVersion)
                {
                    mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        MessageBox.Show("This version of Linkora is outdated.", "Outdated software", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }
            }
        }
    }
}
