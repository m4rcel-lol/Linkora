using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using WLMClient.Compat;
using WLMClient.Compat.Documents;

using WLMClient.UI.Data.Enums;
using WLMClient.Layout;
using WLMData.Enums;
using WLMData.Data.Packets;
using WLMClient.Locale;
using WLMClient.UI.Data;

using RichTextBox = WLMClient.Compat.RichTextBox;
using BrushConverter = WLMClient.Compat.BrushConverter;

namespace WLMClient.UI.Windows
{
    /// <summary>
    /// Interaction logic for ChatWindow.axaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private DateTime lastMessageReceivedDateTime;
        private UserInfo contactUserInfo;
        private StackPanel emoticonPanel;
        private string lastMessageFrom;
        private bool textInputChanged;
        private bool isMouseHoveringEmoticonPanel;
        private bool isWindowClosing;
        private bool isShiftDown;
        private bool isWritingMessage;

        /// <summary>Lets the ported code keep calling <c>window.Dispatcher.Invoke(...)</c>.</summary>
        public Dispatcher Dispatcher
        {
            get { return Dispatcher.UIThread; }
        }

        public ChatWindow(UserInfo userInfo)
        {
            InitializeComponent();

            WireEvents();

            contactUserInfo = userInfo;
            lastMessageReceivedDateTime = DateTime.Now;
            isMouseHoveringEmoticonPanel = false;
            textInputChanged = false;
            isWindowClosing = false;
            isShiftDown = false;
            isWritingMessage = false;
            lastMessageFrom = "";

            this.Icon = MainWindow.LoadWindowIcon("avares://WLMClient/Content/Icons/46.png");

            LoadEmoticonPanel();

            btnSmiley.Source = LoadResource.chatWindowButtonSmileys(ButtonState.None);
            btnNudge.Source = LoadResource.chatWindowButtonNudge(ButtonState.None);

            background.Source = Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_SKINNY);

            txtChat.Document.Blocks.Clear();

            ApplyLanguage();
            Language.Changed += ApplyLanguage;
            Closed += (sender, e) => Language.Changed -= ApplyLanguage;

            UpdatePersonal();
            UpdateContact(userInfo);

            Thread threadParseInputText = new Thread(TextInputParser);
            threadParseInputText.IsBackground = true;
            threadParseInputText.Start();
        }


        private void WireEvents()
        {
            SizeChanged += Window_SizeChanged;
            Closing += Window_Closing;
            Loaded += Window_Loaded;

            AddHandler(InputElement.PointerPressedEvent, Window_PreviewMouseDown, RoutingStrategies.Tunnel);

            btnGame.Click += btnGame_Click;

            txtSend.TextChanged += txtSend_TextChanged;
            txtSend.PreviewKeyUp += txtSend_PreviewKeyUp;
            txtSend.PreviewKeyDown += txtSend_PreviewKeyDown;

            btnSmiley.PointerEntered += btnSmiley_MouseEnter;
            btnSmiley.PointerExited += btnSmiley_MouseLeave;
            btnSmiley.PointerPressed += btnSmiley_PreviewMouseLeftButtonDown;
            btnSmiley.PointerReleased += btnSmiley_PreviewMouseLeftButtonUp;

            btnNudge.PointerEntered += btnNudge_MouseEnter;
            btnNudge.PointerExited += btnNudge_MouseLeave;
            btnNudge.PointerPressed += btnNudge_PreviewMouseLeftButtonDown;
            btnNudge.PointerReleased += btnNudge_PreviewMouseLeftButtonUp;

            imageUserFrame.PointerPressed += imageUserFrame_PreviewMouseDown;
            imageUserFrame.PointerReleased += imageUserFrame_PreviewMouseUp;
            imageUserFrame.PointerExited += imageUserFrame_MouseLeave;

            btnAttach.PointerEntered += btnAttach_MouseEnter;
            btnAttach.PointerExited += btnAttach_MouseLeave;
            btnAttach.PointerReleased += btnAttach_PreviewMouseLeftButtonUp;
        }

        #region Attachments

        private void btnAttach_MouseEnter(object sender, PointerEventArgs e)
        {
            btnAttach.BorderBrush = new BrushConverter().ConvertFrom("#AFAFAF");
            btnAttach.Background = new BrushConverter().ConvertFrom("#EDEDED");
        }

        private void btnAttach_MouseLeave(object sender, PointerEventArgs e)
        {
            btnAttach.BorderBrush = Brushes.Transparent;
            btnAttach.Background = Brushes.Transparent;
        }

        private async void btnAttach_PreviewMouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Language.Format("chat.sendfile.title", contactUserInfo.name),
                    AllowMultiple = true
                });

            if (files == null)
            {
                return;
            }

            foreach (IStorageFile file in files)
            {
                SendAttachment(file);
            }
        }

        private void SendAttachment(IStorageFile file)
        {
            string path = file.TryGetLocalPath();

            if (path == null)
            {
                MessageBox.Show(Language.Get("chat.file.unreadable"), Language.Get("chat.file.failed.title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            try
            {
                FileInfo info = new FileInfo(path);

                if (info.Length > FileTransfer.MaximumSize)
                {
                    MessageBox.Show(
                        Language.Format("chat.file.toolarge.text", info.Name,
                            Attachments.DescribeSize(info.Length),
                            Attachments.DescribeSize(FileTransfer.MaximumSize)),
                        Language.Get("chat.file.toolarge.title"), MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                byte[] data = File.ReadAllBytes(path);

                Network.Client.SendFile(contactUserInfo.id, info.Name, data);

                Conversations.Record(contactUserInfo.id, Personal.USER_INFO.name, info.Name);

                AddAttachmentMessage(Personal.USER_INFO.name, info.Name, path, data.Length);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Language.Format("chat.file.failed.text", exception.Message),
                    Language.Get("chat.file.failed.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Saves an incoming attachment and shows it in the conversation.</summary>
        public void AddReceivedFile(FileTransfer fileTransfer)
        {
            lastMessageReceivedDateTime = DateTime.Now;

            try
            {
                string path = Attachments.Save(fileTransfer.fileName, fileTransfer.data);

                AddAttachmentMessage(contactUserInfo.name, Path.GetFileName(path), path,
                    fileTransfer.data == null ? 0 : fileTransfer.data.Length);
            }
            catch (Exception exception)
            {
                AddNudgeMessage(Language.Format("chat.file.notsaved", contactUserInfo.name, exception.Message));
            }
        }

        /// <summary>
        /// Renders an attachment in the transcript: images inline, anything else as a clickable
        /// name. Follows the same "X says" grouping as ordinary messages.
        /// </summary>
        private void AddAttachmentMessage(string from, string fileName, string path, long size)
        {
            if (lastMessageFrom != from)
            {
                Paragraph txtFrom = new Paragraph();

                txtFrom.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                txtFrom.Inlines.Add(Language.Format("chat.says", from));
                txtFrom.TextAlignment = TextAlignment.Justify;
                txtFrom.Foreground = new BrushConverter().ConvertFrom("#A5A5A5");
                txtFrom.FontSize = 14;
                txtFrom.FontWeight = FontWeight.Normal;
                txtFrom.Margin = new Thickness(0, 24, 0, 0);
                txtFrom.Padding = new Thickness(0, 0, 0, 0);
                txtFrom.LineHeight = 0.1;

                txtChat.Document.Blocks.Add(txtFrom);
                TextParser.ProcessInlines(txtChat, txtFrom.Inlines, false);
            }

            Paragraph txtText = new Paragraph();

            Image paragraphDot = LoadResource.getParagraphRectangle();
            paragraphDot.Margin = new Thickness(0, 0, 4, 2);

            txtText.Inlines.Add(new InlineUIContainer(paragraphDot));
            txtText.Inlines.Add(new InlineUIContainer(CreateAttachmentLink(fileName, path, size))
            {
                BaselineAlignment = BaselineAlignment.TextBottom
            });

            txtText.LineHeight = 0.1;
            txtText.Margin = new Thickness(12, 3, 0, 0);

            txtChat.Document.Blocks.Add(txtText);

            if (Attachments.IsImage(fileName))
            {
                Control preview = CreateImagePreview(path);

                if (preview != null)
                {
                    Paragraph previewParagraph = new Paragraph();

                    previewParagraph.Inlines.Add(new InlineUIContainer(preview));
                    previewParagraph.Margin = new Thickness(12, 4, 0, 0);

                    txtChat.Document.Blocks.Add(previewParagraph);
                }
            }

            txtChat.ScrollToEnd();

            lastMessageFrom = from;
        }

        private Control CreateAttachmentLink(string fileName, string path, long size)
        {
            TextBlock block = new TextBlock();

            block.Text = fileName + " (" + Attachments.DescribeSize(size) + ")";
            block.Cursor = new Cursor(StandardCursorType.Hand);

            // Without a background the text block is not hit testable and cannot be clicked.
            block.Background = Brushes.Transparent;
            block.TextDecorations = TextDecorations.Underline;
            block.Foreground = Brushes.Blue;
            block.FontSize = 13;

            ToolTip.SetTip(block, path);

            block.PointerPressed += (sender, args) => Attachments.Open(path);

            return block;
        }

        /// <summary>A bounded thumbnail of an image attachment, or null if it cannot be decoded.</summary>
        private Control CreateImagePreview(string path)
        {
            try
            {
                Avalonia.Media.Imaging.Bitmap bitmap = new Avalonia.Media.Imaging.Bitmap(path);

                Image image = new Image();

                image.Source = bitmap;
                image.Stretch = Stretch.Uniform;
                image.MaxWidth = 220;
                image.MaxHeight = 165;
                image.Cursor = new Cursor(StandardCursorType.Hand);

                image.PointerPressed += (sender, args) => Attachments.Open(path);

                return image;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        /// <summary>Applies the current language to this conversation window.</summary>
        private void ApplyLanguage()
        {
            btnGame.Content = Language.Get("chat.games");

            ToolTip.SetTip(btnSmiley, Language.Get("chat.smilies"));
            ToolTip.SetTip(btnNudge, Language.Get("chat.nudge"));
            ToolTip.SetTip(btnAttach, Language.Get("chat.sendfile"));

            txtStatus.Text = "(" + Language.GetStatus((UserStatus)contactUserInfo.status) + ")";
        }

        public string GetContactID()
        {
            return contactUserInfo.id;
        }

        public string GetContactName()
        {
            return contactUserInfo.name;
        }

        private void TextInputParser()
        {
            while (!isWindowClosing)
            {
                if (textInputChanged)
                {
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        TextParser.ParseText(txtSend, false);
                    }));

                    textInputChanged = false;
                }

                Thread.Sleep(666);
            }
        }

        public void AddChatMessage(string from, string messageText)
        {
            if (from == contactUserInfo.name)
            {
                txtLastUpdate.Document.Blocks.Clear();
                txtLastUpdate.Document.Blocks.Add(new Paragraph(new Run(Language.Format("chat.lastmessage",
                    lastMessageReceivedDateTime.ToShortTimeString(),
                    lastMessageReceivedDateTime.ToShortDateString()))));
            }

            Paragraph txtFrom = new Paragraph();
            Paragraph txtText = new Paragraph();

            txtFrom.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

            txtFrom.Inlines.Add(Language.Format("chat.says", from));
            txtFrom.TextAlignment = TextAlignment.Justify;
            txtFrom.Foreground = new BrushConverter().ConvertFrom("#A5A5A5");
            txtFrom.FontSize = 14;
            txtFrom.FontWeight = FontWeight.Normal;
            txtFrom.Margin = new Thickness(0, 24, 0, 0);
            txtFrom.Padding = new Thickness(0, 0, 0, 0);
            txtFrom.LineHeight = 0.1;

            Image paragraphDot = LoadResource.getParagraphRectangle();

            // WPF hung this bullet to the left of the text using a -20 margin, which worked because
            // the flow document added its own page padding. Avalonia lays the embedded control out
            // inside the paragraph, so a negative margin pushes it out of view; a small trailing
            // gap puts the bullet in the same place on screen.
            paragraphDot.Margin = new Thickness(0, 0, 4, 2);

            txtText.Inlines.Add(new InlineUIContainer(paragraphDot));

            txtText.Inlines.Add(messageText.TrimEnd());
            txtText.LineHeight = 0.1;
            txtText.Margin = new Thickness(12, 3, 0, 0);

            if (lastMessageFrom != from)
            {
                txtChat.Document.Blocks.Add(txtFrom);
                TextParser.ProcessInlines(txtChat, txtFrom.Inlines, false);
            }

            txtChat.Document.Blocks.Add(txtText);
            TextParser.ProcessInlines(txtChat, txtText.Inlines, true);

            txtChat.ScrollToEnd();

            lastMessageFrom = from;
        }

        public void IsWritingAMessage(bool value)
        {
            if (value)
            {
                txtLastUpdate.Document.Blocks.Clear();

                Paragraph lastUpdateParagraph = new Paragraph(new Run(Language.Format("chat.writing", contactUserInfo.name)));

                txtLastUpdate.Document.Blocks.Add(lastUpdateParagraph);

                TextParser.ProcessInlines(txtLastUpdate, lastUpdateParagraph.Inlines, false);
            }
            else
            {
                txtLastUpdate.Document.Blocks.Clear();
                txtLastUpdate.Document.Blocks.Add(new Paragraph(new Run(Language.Format("chat.lastmessage",
                    lastMessageReceivedDateTime.ToShortTimeString(),
                    lastMessageReceivedDateTime.ToShortDateString()))));
            }
        }

        public void UpdatePersonal()
        {
            imageUserAvatar.Source = LoadResource.GetDefaultAvatarImage();
            imageUserFrame.Source = LoadResource.GetAvatarFrameFromStatus((UserStatus)Personal.USER_INFO.status, AvatarSize.Big);

            if (Personal.USER_INFO.avatar != "")
            {
                Avalonia.Media.Imaging.Bitmap image = LoadResource.GetAvatar(Personal.USER_INFO.avatar);
                if (image != null)
                {
                    imageUserAvatar.Source = image;
                }
                else
                {
                    imageUserAvatar.Source = LoadResource.GetDefaultAvatarImage();
                }
            }
            else
            {
                imageUserAvatar.Source = LoadResource.GetDefaultAvatarImage();
            }
        }

        public void UpdateContact(UserInfo userInfo)
        {
            txtName.Text = userInfo.name;
            TextParser.ParseText(txtName, false);
            this.Title = userInfo.name;
            txtStatus.Text = "(" + Language.GetStatus((UserStatus)userInfo.status) + ")";

            if (userInfo.status == (int)UserStatus.Offline || Personal.USER_INFO.status == (int)UserStatus.Offline || userInfo.blocked == true)
            {
                this.IsEnabled = false;
            }
            else
            {
                this.IsEnabled = true;
            }

            if (userInfo.avatar != "")
            {
                Avalonia.Media.Imaging.Bitmap image = LoadResource.GetAvatar(userInfo.avatar);
                if (image != null)
                {
                    imagePartnerAvatar.Source = image;
                }
                else
                {
                    imagePartnerAvatar.Source = LoadResource.GetDefaultAvatarImage();
                }
            }
            else
            {
                imagePartnerAvatar.Source = LoadResource.GetDefaultAvatarImage();
            }

            imagePartnerFrame.Source = LoadResource.GetAvatarFrameFromStatus((UserStatus)userInfo.status, AvatarSize.Big);

            contactUserInfo = userInfo;
        }

        private void LoadEmoticonPanel()
        {
            emoticonPanel = new StackPanel();

            emoticonPanel.Background = Brushes.White;
            emoticonPanel.Margin = new Thickness(151, 0, 0, 73);
            emoticonPanel.VerticalAlignment = VerticalAlignment.Bottom;
            emoticonPanel.HorizontalAlignment = HorizontalAlignment.Left;

            Controls.Emoticons emoticonPage = new Controls.Emoticons(txtSend);

            emoticonPage.PropertyChanged += (sender, args) =>
            {
                if (args.Property == Visual.IsVisibleProperty)
                {
                    EmoticonPageVisibilityChanged((bool)args.NewValue);
                }
            };

            emoticonPanel.Children.Add(emoticonPage);

            emoticonPanel.PointerEntered += Emoticons_MouseEnter;
            emoticonPanel.PointerExited += Emoticons_MouseLeave;
        }

        private void EmoticonPageVisibilityChanged(bool isVisible)
        {
            if (!isVisible)
            {
                window.Children.Remove(emoticonPanel);

                emoticonPanel.Children[0].IsVisible = true;
            }
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (!isWindowClosing)
            {
                isWindowClosing = true;

                ManageChatWindows.RemoveChatWindow(this);
            }
        }

        public void CloseWindow()
        {
            if (!isWindowClosing)
            {
                isWindowClosing = true;

                ManageChatWindows.RemoveChatWindow(this);
            }

            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Window_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            if (emoticonPanel != null)
            {
                if (!isMouseHoveringEmoticonPanel)
                {
                    window.Children.Remove(emoticonPanel);
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void btnGame_Click(object sender, RoutedEventArgs e)
        {

        }

        private void imageUserFrame_PreviewMouseUp(object sender, PointerReleasedEventArgs e)
        {

        }

        private void imageUserFrame_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {

        }

        private void imageUserFrame_MouseLeave(object sender, PointerEventArgs e)
        {

        }

        private void txtSend_TextChanged(object sender, EventArgs e)
        {
            textInputChanged = true;

            if (!txtSend.Document.IsEmpty)
            {
                if (!isWritingMessage)
                {
                    Network.Client.SendWritingStatus(contactUserInfo.id, true);

                    isWritingMessage = true;
                }
            }
            else
            {
                Network.Client.SendWritingStatus(contactUserInfo.id, false);
                isWritingMessage = false;
            }
        }

        private void txtSend_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift | e.Key == Key.RightShift)
            {
                isShiftDown = false;
            }

            if (e.Key == Key.Enter & !isShiftDown)
            {
                string txtSendChat = TextParser.GetPlainText(txtSend.Document);

                if (txtSendChat.Length == 0 || txtSendChat.Trim() == "")
                {
                    return;
                }

                AddChatMessage(Personal.USER_INFO.name, txtSendChat);

                Conversations.Record(contactUserInfo.id, Personal.USER_INFO.name, txtSendChat);

                Network.Client.SendMessage(new Message(contactUserInfo.id, txtSendChat));

                txtSend.Document.Blocks.Clear();
            }
        }

        private void txtSend_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift | e.Key == Key.RightShift)
            {
                isShiftDown = true;
            }

            if (e.Key == Key.Enter & !isShiftDown)
            {
                e.Handled = true;
            }

            if (e.Key == Key.Enter & isShiftDown)
            {
                InsertText("\n", txtSend);

                e.Handled = true;
            }
        }

        private void InsertText(String text, RichTextBox rtb)
        {
            rtb.InsertText(text);
        }

        private void btnSmiley_MouseEnter(object sender, PointerEventArgs e)
        {
            btnSmiley.Source = LoadResource.chatWindowButtonSmileys(ButtonState.Hover);
        }

        private void btnSmiley_MouseLeave(object sender, PointerEventArgs e)
        {
            btnSmiley.Source = LoadResource.chatWindowButtonSmileys(ButtonState.None);
        }

        private void btnSmiley_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            btnSmiley.Source = LoadResource.chatWindowButtonSmileys(ButtonState.Pressed);
        }

        private void btnSmiley_PreviewMouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
        {
            btnSmiley.Source = LoadResource.chatWindowButtonSmileys(ButtonState.Hover);

            if (!window.Children.Contains(emoticonPanel))
            {
                window.Children.Add(emoticonPanel);
            }
        }

        private void Emoticons_MouseLeave(object sender, PointerEventArgs e)
        {
            isMouseHoveringEmoticonPanel = false;
        }

        private void Emoticons_MouseEnter(object sender, PointerEventArgs e)
        {
            isMouseHoveringEmoticonPanel = true;
        }

        private void btnNudge_MouseEnter(object sender, PointerEventArgs e)
        {
            btnNudge.Source = LoadResource.chatWindowButtonNudge(ButtonState.Hover);
        }

        private void btnNudge_MouseLeave(object sender, PointerEventArgs e)
        {
            btnNudge.Source = LoadResource.chatWindowButtonNudge(ButtonState.None);
        }

        private void btnNudge_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            btnNudge.Source = LoadResource.chatWindowButtonNudge(ButtonState.Pressed);
        }

        private void btnNudge_PreviewMouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
        {
            btnNudge.Source = LoadResource.chatWindowButtonNudge(ButtonState.Hover);

            AddNudgeMessage(Language.Get("chat.nudge.sent"));
            Network.Client.SendNudge(contactUserInfo.id);
        }

        public void AddNudgeMessage(string text)
        {
            lastMessageFrom = "";

            Paragraph txtText = new Paragraph();
            txtText.Inlines.Add(text);
            txtText.TextAlignment = TextAlignment.Justify;
            txtText.Foreground = new BrushConverter().ConvertFrom("#29292B");
            txtText.FontSize = 13;
            txtText.FontWeight = FontWeight.Normal;
            txtText.LineHeight = 18;
            txtText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            txtText.Margin = new Thickness(0, 10, 0, 0);
            txtText.Padding = new Thickness(5);

            txtText.BorderBrush = new BrushConverter().ConvertFrom("#eaeaea");
            txtText.BorderThickness = new Thickness(0, 2, 0, 2);

            txtChat.Document.Blocks.Add(txtText);
            TextParser.ProcessInlines(txtChat, txtText.Inlines, false);

            txtChat.ScrollToEnd();
        }

        public void Nudge()
        {
            AddNudgeMessage(Language.Format("chat.nudge.received", contactUserInfo.name));

            if (Personal.USER_INFO.status == 3 & this.WindowState != WindowState.Maximized)
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }

                this.Activate();

                Resource.Sounds.Player.PlaySound(Resource.Sounds.Identifiers.NUDGE);

                Thread threadNudge = new Thread(NudgeWindow);
                threadNudge.IsBackground = true;
                threadNudge.Start();
            }
        }

        public void NudgeWindow()
        {
            for (int i = 0; i < 25; i++)
            {
                moveWindow(5, 0);
                Thread.Sleep(10);

                moveWindow(0, 5);
                Thread.Sleep(10);

                moveWindow(-5, 0);
                Thread.Sleep(10);

                moveWindow(0, -5);
                Thread.Sleep(10);
            }
        }

        public void moveWindow(int top, int left)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                this.Position = new PixelPoint(this.Position.X + left, this.Position.Y + top);
            }));
        }
    }
}
