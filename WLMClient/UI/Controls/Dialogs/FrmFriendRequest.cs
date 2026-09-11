using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using WLMData.Data.Packets;
using WLMClient.Layout;
using WLMClient.Locale;
using WLMClient.Network;

namespace WLMClient.UI.Controls.Dialogs
{
    /// <summary>The incoming friend request prompt, laid out as the Windows Forms designer had it.</summary>
    public class FrmFriendRequest : DialogWindow
    {
        private string userID = "";
        private bool friendRequestAnswered = false;

        private Image imgUserAvatar;
        private TextBlock lblText;
        private Button btnYes;
        private Button btnNo;

        public FrmFriendRequest(UserInfo userInfo) : base(599, 199, Language.Get("friendrequest.title"))
        {
            Topmost = true;

            InitializeComponent();

            Closing += frmFriendRequest_FormClosing;

            userID = userInfo.id;
            lblText.Text = Language.Format("friendrequest.text", userInfo.name, userInfo.id);

            CroppedBitmap defaultImg = LoadResource.GetDefaultAvatarImage();

            if (userInfo.avatar != "")
            {
                Bitmap userImg = LoadResource.GetAvatar(userInfo.avatar);

                if (userImg != null)
                {
                    imgUserAvatar.Source = userImg;
                }
                else
                {
                    imgUserAvatar.Source = defaultImg;
                }
            }
            else
            {
                imgUserAvatar.Source = defaultImg;
            }
        }

        private void InitializeComponent()
        {
            AddGroupBox(Language.Get("friendrequest.title"), 10, 11, 579, 176);

            Border avatarBorder = new Border
            {
                Width = 100,
                Height = 100,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x82, 0x82, 0x82))
            };

            imgUserAvatar = new Image { Stretch = Stretch.Uniform };
            avatarBorder.Child = imgUserAvatar;

            Add(avatarBorder, 21, 37);

            lblText = CreateLabel("label1", "Times New Roman, Times, serif", 12, Brushes.Black);
            Add(lblText, 129, 39);

            btnYes = CreateButton(Language.Get("dialog.yes"), 110, 31, Brushes.WhiteSmoke, Brushes.Black);
            btnYes.Click += btnYes_Click;
            Add(btnYes, 357, 150);

            btnNo = CreateButton(Language.Get("dialog.no"), 110, 31, Brushes.WhiteSmoke, Brushes.Black);
            btnNo.Click += btnNo_Click;
            Add(btnNo, 473, 150);
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            friendRequestAnswered = true;

            Client.SendFriendRequestResponse(userID, WLMData.Enums.FriendRequestResponseCode.decline);

            this.Close();
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            friendRequestAnswered = true;

            Client.SendFriendRequestResponse(userID, WLMData.Enums.FriendRequestResponseCode.accept);

            this.Close();
        }

        private void frmFriendRequest_FormClosing(object sender, WindowClosingEventArgs e)
        {
            if (friendRequestAnswered == false)
            {
                friendRequestAnswered = true;

                Client.SendFriendRequestResponse(userID, WLMData.Enums.FriendRequestResponseCode.decline);
            }
        }
    }
}
