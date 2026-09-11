using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.VisualTree;

using WLMClient.Compat;
using WLMClient.Compat.Documents;

using WLMData.Data.Packets;
using WLMClient.UI.Data;
using WLMClient.UI.Data.Enums;
using WLMClient.Locale;
using WLMData.Enums;
using WLMClient.Layout;
using WLMClient.UI.Controls.Dialogs;
using WLMClient.Network;

using RichTextBox = WLMClient.Compat.RichTextBox;
using BrushConverter = WLMClient.Compat.BrushConverter;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// Interaction logic for Main.axaml
    /// </summary>
    public partial class Main : UserControl
    {
        private List<ContactListEntryData> listContacts;
        private bool isEnterKeyDownInComment;
        private bool isCommentEditSubmitted;
        private bool isCommentBeingEdited;

        public Main()
        {
            listContacts = new List<ContactListEntryData>();

            InitializeComponent();

            WireEvents();

            UpdatePersonalInformation();

            menuItemIconArrowAvailable.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Available);
            menuItemIconArrowBusy.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Busy);
            menuItemIconArrowAway.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Away);
            menuItemIconArrowOffline.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Offline);

            isEnterKeyDownInComment = false;
            isCommentEditSubmitted = false;
            isCommentBeingEdited = false;
        }


        private void WireEvents()
        {
            btnArrowStatus.PointerPressed += btnArrowStatus_PreviewMouseLeftButtonDown;

            // Escape has to be intercepted before the text box acts on it, hence the tunnelling.
            txtQuickMessage.AddHandler(InputElement.KeyDownEvent, txtQuickMessage_PreviewKeyDown,
                RoutingStrategies.Tunnel);
            txtQuickMessage.KeyUp += txtQuickMessage_PreviewKeyUp;
            txtQuickMessage.PointerPressed += txtQuickMessage_PreviewMouseLeftButtonDown;
            txtQuickMessage.LostFocus += txtQuickMessage_LostKeyboardFocus;
            txtQuickMessage.GotFocus += txtQuickMessage_GotKeyboardFocus;

            btnAddFriend.Click += btnAddFriend_Click;
            btnAddFriend.PointerEntered += btnAddFriend_MouseEnter;
            btnAddFriend.PointerExited += btnAddFriend_MouseLeave;

            menuItemArrowAvailable.Click += menuItemArrowAvailable_Click;
            menuItemArrowBusy.Click += menuItemArrowBusy_Click;
            menuItemArrowAway.Click += menuItemArrowAway_Click;
            menuItemArrowOffline.Click += menuItemArrowOffline_Click;
            menuItemArrowOptions.Click += menuItemArrowOptions_Click;
            menuItemArrowExit.Click += menuItemArrowExit_Click;
        }

        private void btnArrowStatus_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            contextMenuArrow.Open(btnArrowStatus);
        }

        private void txtQuickMessage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                isEnterKeyDownInComment = true;
                isCommentBeingEdited = false;
            }

            if (e.Key == Key.Escape)
            {
                ResetComment();
                isCommentBeingEdited = false;
                e.Handled = true;
            }
        }

        private void txtQuickMessage_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (isEnterKeyDownInComment)
            {
                isCommentEditSubmitted = true;
                txtQuickMessage.SelectionStart = 0;
                Keyboard.ClearFocus(this);
                Personal.USER_INFO.comment = (txtQuickMessage.Text ?? "").Trim();
                isEnterKeyDownInComment = false;

                Client.SendUserUpdate();
                UpdatePersonalInformation();

                isCommentBeingEdited = false;

                ResetComment();
            }
        }

        private void txtQuickMessage_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            if (Personal.USER_INFO.comment.Length == 0)
            {
                txtQuickMessage.Clear();
                txtQuickMessage.Focus();
            }

            isCommentBeingEdited = true;
        }

        private void txtQuickMessage_LostKeyboardFocus(object sender, RoutedEventArgs e)
        {
            ResetComment();
        }

        public void ResetComment()
        {
            if (!isCommentEditSubmitted)
            {
                if (Personal.USER_INFO.comment.Length == 0)
                {
                    txtQuickMessage.Text = "Share a quick message";
                }
                else
                {
                    txtQuickMessage.Text = Personal.USER_INFO.comment;
                }
            }

            Keyboard.ClearFocus(this);
            isCommentBeingEdited = false;
            isCommentEditSubmitted = false;
        }

        public void WindowSizeChanged(double width, double height)
        {
            if (width >= 555)
            {
                background.Source = Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_WIDE);
            }

            if (width < 555)
            {
                background.Source = Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_SKINNY);
            }

            foreach (ContactListEntryData contact in listContacts)
            {
                contact.richTextBox.Width = listContactBorder.Bounds.Width - 2;
            }

            mainControl.Height = height;
        }

        public void UpdatePersonalInformation()
        {
            txtName.Text = Personal.USER_INFO.name;
            txtStatus.Text = "(" + ((UserStatus)Personal.USER_INFO.status).ToString() + ")";
            txtConnectedTo.Text = "Connected to " + Config.Properties.GetServerDisplay();

            if (Personal.USER_INFO.comment.Length != 0)
            {
                txtQuickMessage.Text = Personal.USER_INFO.comment;
            }
            else
            {
                txtQuickMessage.Text = "Share a quick message";
            }

            imagePartnerFrame.Source = LoadResource.GetAvatarFrameFromStatus((UserStatus)Personal.USER_INFO.status, AvatarSize.Small);

            if (Personal.USER_INFO.avatar != "")
            {
                Bitmap image = LoadResource.GetAvatar(Personal.USER_INFO.avatar);

                if (image != null)
                {
                    imagePartnerAvatar.Source = LoadResource.Resize(image, 50, 50, BitmapInterpolationMode.HighQuality);
                }
                else
                {
                    imagePartnerAvatar.Source = LoadResource.Resize(LoadResource.GetDefaultAvatarImage(), 50, 50, BitmapInterpolationMode.HighQuality);
                }
            }
            else
            {
                imagePartnerAvatar.Source = LoadResource.Resize(LoadResource.GetDefaultAvatarImage(), 50, 50, BitmapInterpolationMode.HighQuality);
            }

            Window mainWindow = MessageBox.GetActiveWindow();
            UI.Windows.MainWindow messengerWindow = mainWindow as UI.Windows.MainWindow;

            if (messengerWindow == null)
            {
                messengerWindow = this.GetVisualRoot() as UI.Windows.MainWindow;
            }

            if (messengerWindow != null)
            {
                if (Personal.USER_INFO.status == 0) // Offline
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_OFFLINE);
                }

                if (Personal.USER_INFO.status == 1) // Busy
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_BUSY);
                }

                if (Personal.USER_INFO.status == 2) // Away
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_AWAY);
                }

                if (Personal.USER_INFO.status == 3) // Available
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_AVAILABLE);
                }
            }

            TextParser.ParseText(txtQuickMessage, false);
            TextParser.ParseText(txtName, false);

            ManageChatWindows.UpdateChatWindowPersonal();
        }

        public void AddContactToList(UserInfo contact)
        {
            RichTextBox txtContact = new RichTextBox();
            txtContact.DoubleTapped += TxtContact_PreviewMouseDoubleClick;

            txtContact.IsDocumentEnabled = true;

            ContextMenu context = new ContextMenu();
            MenuItem blockItem = new MenuItem(); blockItem.Tag = contact.id;
            MenuItem deleteItem = new MenuItem(); deleteItem.Tag = contact.id;
            MenuItem openChatItem = new MenuItem(); openChatItem.Tag = contact.id;

            if (contact.blocked)
            {
                blockItem.Header = "Unblock";
            }
            else
            {
                blockItem.Header = "Block";
            }

            openChatItem.Header = "Send Message";
            deleteItem.Header = "Remove Contact";

            context.Items.Add(openChatItem);
            context.Items.Add(new Separator());
            context.Items.Add(blockItem);
            context.Items.Add(deleteItem);

            blockItem.Click += BlockItem_Click;
            deleteItem.Click += DeleteItem_Click;
            openChatItem.Click += OpenChatItem_Click;

            txtContact.ContextMenu = context;

            txtContact.PointerPressed += txtContact_PreviewMouseDown;
            txtContact.PointerEntered += txtContact_MouseEnter;
            txtContact.PointerExited += txtContact_MouseLeave;
            txtContact.Document.Blocks.Clear();
            txtContact.Margin = new Thickness(0, 0, 0, 2);
            txtContact.BorderThickness = new Thickness(0);
            txtContact.Background = new BrushConverter().ConvertFrom("#FCFCFC");
            txtContact.VerticalAlignment = VerticalAlignment.Top;
            txtContact.IsReadOnly = true;
            txtContact.Height = 30;
            txtContact.Tag = contact.id;
            txtContact.ToolTip = contact.id;
            txtContact.Cursor = new Cursor(StandardCursorType.Arrow);

            Image imgStatus = new Image();
            imgStatus.Source = LoadResource.GetSmallIconFromStatus((UserStatus)contact.status);
            imgStatus.Width = 16;
            imgStatus.Height = 16;
            imgStatus.Stretch = Stretch.Fill;
            imgStatus.Margin = new Thickness(0, 4, 0, 0);

            RenderOptions.SetBitmapInterpolationMode(imgStatus, BitmapInterpolationMode.None);
            RenderOptions.SetEdgeMode(imgStatus, EdgeMode.Aliased);
            InlineUIContainer container = new InlineUIContainer(imgStatus);

            Paragraph paragraph = new Paragraph(container);
            paragraph.Padding = new Thickness(0, 0, 0, 0);
            paragraph.Margin = new Thickness(6, 0, 0, 0);
            paragraph.TextAlignment = TextAlignment.Left;

            txtContact.Document.Blocks.Add(paragraph);

            TextBlock txtName = new TextBlock();

            if (contact.comment.Length > 0)
            {
                txtName.Text = contact.name + " - ";
            }
            else
            {
                txtName.Text = contact.name;
            }

            txtName.Margin = new Thickness(5, 0, 0, 1);
            txtName.FontFamily = new FontFamily("Segoe UI");
            txtName.FontSize = 12;
            txtName.Foreground = new BrushConverter().ConvertFrom("#333333");
            txtName.FontWeight = FontWeight.Normal;

            paragraph.Inlines.Add(new InlineUIContainer(txtName)
            {
                BaselineAlignment = BaselineAlignment.TextBottom
            });

            TextParser.ParseText(txtName, false);

            TextBlock txtQuickMessage = new TextBlock();

            txtQuickMessage.Text = contact.comment;
            txtQuickMessage.Margin = new Thickness(5, 0, 0, 1);
            txtQuickMessage.FontFamily = new FontFamily("Segoe UI");
            txtQuickMessage.FontSize = 12;
            txtQuickMessage.Foreground = new BrushConverter().ConvertFrom("#888888");
            txtQuickMessage.FontWeight = FontWeight.Normal;

            paragraph.Inlines.Add(new InlineUIContainer(txtQuickMessage)
            {
                BaselineAlignment = BaselineAlignment.TextBottom
            });

            TextParser.ParseText(txtQuickMessage, true);

            listContacts.Add(new ContactListEntryData(txtContact, imgStatus, txtName, txtQuickMessage));

            if (contact.status != 0)
            {
                contactListView.Items.Insert(0, txtContact);
            }
            else
            {
                contactListView.Items.Insert(contactListView.Items.Count, txtContact);
            }
        }

        private void TxtContact_PreviewMouseDoubleClick(object sender, TappedEventArgs e)
        {
            foreach (UserInfo contact in Personal.USER_CONTACTS)
            {
                if (contact.id.ToString() == ((RichTextBox)sender).Tag.ToString())
                {
                    OpenChatWindow(contact);

                    e.Handled = true;

                    break;
                }
            }
        }

        public void OpenChatWindow(UserInfo userInfo)
        {
            Windows.ChatWindow chatWindow = ManageChatWindows.GetChatWindow(userInfo.id);
            if (chatWindow != null)
            {
                chatWindow.Activate();
                chatWindow.Focus();
            }
        }

        private void OpenChatItem_Click(object sender, RoutedEventArgs e)
        {
            Windows.ChatWindow chatWindow = ManageChatWindows.GetChatWindow(((MenuItem)sender).Tag.ToString());
            if (chatWindow != null)
            {
                chatWindow.Activate();
                chatWindow.Focus();
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            Client.DeleteContact(((MenuItem)sender).Tag.ToString());
        }

        public void RemoveContact(string userID)
        {
            lock (contactListView)
            {
                for (int i = 0; i < contactListView.Items.Count; i++)
                {
                    if (contactListView.Items[i] is RichTextBox)
                    {
                        if (((RichTextBox)contactListView.Items[i]).Tag.ToString() == userID)
                        {
                            ManageChatWindows.RemoveChatWindow(userID);

                            contactListView.Items.Remove(((RichTextBox)contactListView.Items[i]));
                            UpdateContactCount();

                            ContactListEntryData contactListData = null;
                            UserInfo user = null;
                            foreach (ContactListEntryData contactData in listContacts)
                            {
                                if (contactData.richTextBox.Tag.ToString() == userID)
                                {
                                    contactListData = contactData;
                                    break;
                                }
                            }

                            foreach (UserInfo userData in Personal.USER_CONTACTS)
                            {
                                if (userData.id == userID)
                                {
                                    user = userData;
                                    break;
                                }
                            }

                            if (user != null)
                            {
                                lock (Personal.USER_CONTACTS)
                                {
                                    Personal.USER_CONTACTS.Remove(user);
                                }
                            }

                            if (contactListData != null)
                            {
                                lock (listContacts)
                                {
                                    listContacts.Remove(contactListData);
                                }
                            }

                            UpdateContactCount();

                            break;
                        }
                    }
                }
            }
        }

        private void BlockItem_Click(object sender, RoutedEventArgs e)
        {
            lock (contactListView)
            {
                for (int i = 0; i < contactListView.Items.Count; i++)
                {
                    if (contactListView.Items[i] is RichTextBox)
                    {
                        if (((RichTextBox)contactListView.Items[i]).Tag.ToString() == ((MenuItem)sender).Tag.ToString())
                        {
                            ContextMenu contextMenu = ((RichTextBox)contactListView.Items[i]).ContextMenu;

                            string blockMenuHeaderText = ((MenuItem)contextMenu.Items[2]).Header.ToString();

                            if (blockMenuHeaderText == "Block")
                            {
                                ((MenuItem)contextMenu.Items[2]).Header = "Unblock";
                            }
                            else
                            {
                                ((MenuItem)contextMenu.Items[2]).Header = "Block";
                            }

                            break;
                        }
                    }
                }
            }

            Client.BlockContact(((MenuItem)sender).Tag.ToString());
        }

        void txtContact_MouseLeave(object sender, PointerEventArgs e)
        {
            if (((RichTextBox)sender).IsFocused == false)
            {
                ((RichTextBox)sender).BorderThickness = new Thickness(0);
                ((RichTextBox)sender).Background = Brushes.Transparent;
            }
        }

        void txtContact_MouseEnter(object sender, PointerEventArgs e)
        {
            if (((RichTextBox)sender).IsFocused == false)
            {
                LinearGradientBrush gradientBrush = BrushHelper.VerticalGradient(
                    Color.FromRgb(235, 243, 253), Color.FromRgb(252, 253, 254));

                ((RichTextBox)sender).Background = gradientBrush;
                ((RichTextBox)sender).BorderThickness = new Thickness(1, 1, 1, 1);
                ((RichTextBox)sender).BorderBrush = new BrushConverter().ConvertFrom("#B8D6FB");
            }
        }

        void txtContact_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            foreach (ContactListEntryData contact in listContacts)
            {
                contact.richTextBox.BorderThickness = new Thickness(0);
                contact.richTextBox.Background = Brushes.Transparent;
            }

            LinearGradientBrush gradientBrush = BrushHelper.VerticalGradient(
                Color.FromRgb(235, 244, 254), Color.FromRgb(207, 228, 254));

            ((RichTextBox)sender).Background = gradientBrush;
            ((RichTextBox)sender).BorderThickness = new Thickness(1, 1, 1, 1);
            ((RichTextBox)sender).BorderBrush = new BrushConverter().ConvertFrom("#84ACDD");
        }

        public void UpdateContact(UserInfo userInfo)
        {
            lock (Personal.USER_CONTACTS)
            {
                UserInfo userFound = Personal.USER_CONTACTS.FirstOrDefault(p => p.id == userInfo.id.Trim());

                if (userFound == null)
                {
                    Personal.USER_CONTACTS.Add(userInfo);
                    AddContactToList(userInfo);
                }
                else
                {
                    bool userJustLoggedOn = false;
                    bool userJustLoggedOff = false;

                    ManageChatWindows.UpdateChatWindowUser(userInfo);

                    if (userFound.status != userInfo.status & userFound.status == Convert.ToInt16(UserStatus.Offline)
                        & userInfo.status != Convert.ToInt16(UserStatus.Offline))
                    {
                        userJustLoggedOn = true;
                    }

                    if (userFound.status != userInfo.status & userFound.status != Convert.ToInt16(UserStatus.Offline)
                        & userInfo.status == Convert.ToInt16(UserStatus.Offline))
                    {
                        userJustLoggedOff = true;
                    }

                    userFound.name = userInfo.name;
                    userFound.status = userInfo.status;
                    userFound.avatar = userInfo.avatar;
                    userFound.comment = userInfo.comment;

                    userFound = userInfo;

                    foreach (ContactListEntryData contact in listContacts)
                    {
                        if (contact.richTextBox.ToolTip.ToString() == userFound.id.ToString())
                        {

                            contact.image.Source = LoadResource.GetSmallIconFromStatus((UserStatus)userInfo.status);
                            if (userInfo.comment.Length == 0)
                            {
                                contact.name.Text = userInfo.name;
                            }
                            else
                            {
                                contact.name.Text = userInfo.name + " - ";
                            }


                            if (userJustLoggedOn)
                            {
                                contactListView.Items.Remove(contact.richTextBox);
                                contactListView.Items.Insert(0, contact.richTextBox);
                            }

                            if (userJustLoggedOff)
                            {
                                contactListView.Items.Remove(contact.richTextBox);
                                contactListView.Items.Insert(contactListView.Items.Count, contact.richTextBox);
                            }

                            contact.comment.Text = userInfo.comment;

                            TextParser.ParseText(contact.name, false);
                            TextParser.ParseText(contact.comment, true);

                            break;
                        }
                    }

                    if (userJustLoggedOn & Personal.USER_INFO.status == Convert.ToInt16(UserStatus.Available) & !userInfo.blocked)
                    {
                        Notification.NotificationManager.Showpopup(userInfo.name, "has just signed in.", null);

                        Resource.Sounds.Player.PlaySound(Resource.Sounds.Identifiers.ONLINE);
                    }
                }
            }

            UpdateContactCount();
        }

        public void UpdateContactCount()
        {
            int countOnlineContacts = 0;

            foreach (UserInfo contact in Personal.USER_CONTACTS)
            {
                if ((UserStatus)contact.status != UserStatus.Offline)
                {
                    countOnlineContacts++;
                }
            }

            txtFriends.Text = String.Format("Friends ({0}/{1})", countOnlineContacts, Personal.USER_CONTACTS.Count);
        }

        private void btnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            FrmAddNewFriend frmAddNewFriend = new FrmAddNewFriend();

            frmAddNewFriend.ShowDialog(() => borderAddFriend.BorderThickness = new Thickness(0));
        }

        private void btnAddFriend_MouseEnter(object sender, PointerEventArgs e)
        {
            borderAddFriend.BorderThickness = new Thickness(1);
            borderAddFriend.BorderBrush = new BrushConverter().ConvertFrom("#D6D6D6");
        }

        private void btnAddFriend_MouseLeave(object sender, PointerEventArgs e)
        {
            borderAddFriend.BorderThickness = new Thickness(0);
        }

        private void menuItemArrowBusy_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Busy);
            Client.SendUserUpdate();
        }

        private void menuItemArrowAvailable_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Available);
            Client.SendUserUpdate();
        }

        private void menuItemArrowAway_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Away);
            Client.SendUserUpdate();
        }

        private void menuItemArrowOffline_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Offline);
            Client.SendUserUpdate();
        }

        private void menuItemArrowOptions_Click(object sender, RoutedEventArgs e)
        {
            FrmOptions frmOptions = new FrmOptions();

            frmOptions.ShowDialog(null);
        }

        private void menuItemArrowExit_Click(object sender, RoutedEventArgs e)
        {
            ManageChatWindows.CloseAllOpenChatWindows();

            Environment.Exit(0);
        }

        private void txtQuickMessage_GotKeyboardFocus(object sender, GotFocusEventArgs e)
        {
            if (!isCommentBeingEdited)
            {
                Keyboard.ClearFocus(this);
            }
        }
    }
}
