using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using WLMClient.Locale;
using WLMClient.Network;

namespace WLMClient.UI.Controls.Dialogs
{
    /// <summary>The "Add a Contact" dialog, laid out as the Windows Forms designer had it.</summary>
    public class FrmAddNewFriend : DialogWindow
    {
        private TextBox txtName;
        private Button btnSubmit;
        private TextBlock lblText;

        public FrmAddNewFriend() : base(609, 221, Language.Get("addfriend.title"))
        {
            InitializeComponent();

            Opened += frmAddNewFriend_Load;
        }

        private void InitializeComponent()
        {
            AddGroupBox(Language.Get("addfriend.title"), 15, 14, 579, 192);

            lblText = CreateLabel(Language.Get("addfriend.prompt"), "Times New Roman, Times, serif", 12);
            Add(lblText, 32, 53);

            txtName = new TextBox
            {
                Width = 427,
                Height = 29,
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 14.25 * PointToPixel,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2, 0, 2, 0)
            };

            txtName.KeyUp += txtName_KeyUp;
            Add(txtName, 36, 162);

            btnSubmit = CreateButton(Language.Get("dialog.ok"), 110, 31);
            btnSubmit.Click += btnSubmit_Click;
            btnSubmit.IsDefault = true;
            Add(btnSubmit, 469, 160);
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text ?? "";

            if (name.Length > 0 && (name.Length < 30) &&
                 !name.Contains(" ") && (name.Trim().ToLower() != Personal.USER_INFO.id))
            {
                Client.AddNewContact(name);

                this.Close();
            }
        }

        private void txtName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnSubmit_Click(null, null);
            }
        }

        private void frmAddNewFriend_Load(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => txtName.Focus(), DispatcherPriority.Loaded);
        }
    }
}
