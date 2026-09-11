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
using WLMClient.Config;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Handles the server's answer to a request to change the sign in name.</summary>
    class ReceiveUsernameChange : PacketHandler
    {
        public ReceiveUsernameChange(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<UsernameChangeResult>(
                PacketName.sendUsernameChangeResult.ToString(), Result);
        }

        protected void Result(PacketHeader header, Connection connection, UsernameChangeResult result)
        {
            PostLoginAction(result);
        }

        public override void PostLoginAction(object packet)
        {
            base.PostLoginAction(packet);

            UsernameChangeResult result = (UsernameChangeResult)packet;

            mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                if (result.resultCode != (int)UsernameChangeResultCode.success)
                {
                    MessageBox.Show(DescribeFailure((UsernameChangeResultCode)result.resultCode),
                        Language.Get("options.username.failed.title"),
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                // The id is fixed once a UserInfo is built, so the record is rebuilt around it.
                UserInfo current = Personal.USER_INFO;

                Personal.USER_INFO = new UserInfo(result.username, current.name, current.comment,
                    current.status, current.avatar, current.blocked);

                // Sign in details are saved under the old name, so keep them usable.
                SaveData configuration = SaveDataManager.GetConfiguration();

                if (!string.IsNullOrEmpty(configuration.saveId))
                {
                    configuration.saveId = result.username;

                    SaveDataManager.SaveConfiguration(configuration);
                }

                Favourites.LoadFor(result.username);

                if (!mainWindow.IsPageMainNull())
                {
                    mainWindow.GetMainPage().UpdatePersonalInformation();
                }

                MessageBox.Show(Language.Format("options.username.changed", result.username),
                    Language.Get("options.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }));
        }

        private static string DescribeFailure(UsernameChangeResultCode code)
        {
            switch (code)
            {
                case UsernameChangeResultCode.taken:
                    return Language.Get("options.username.taken");
                case UsernameChangeResultCode.invalid:
                    return Language.Get("options.username.invalid");
                default:
                    return Language.Get("options.username.error");
            }
        }
    }
}
