using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using WLMClient.Layout;
using WLMClient.Locale;
using WLMData.Data.Packets;
using WLMData.Enums;

namespace WLMClient.UI.Windows
{
    /// <summary>How far along a call is.</summary>
    public enum CallState
    {
        /// <summary>Placed, waiting for the other side to pick up.</summary>
        Calling,

        /// <summary>Ringing here, waiting for this user to answer.</summary>
        Ringing,

        Connected,
        Ended
    }

    /// <summary>
    /// The call window. Setting a call up and tearing it down works; carrying the audio and video
    /// does not exist yet, so the camera and screen sharing buttons say so rather than pretending.
    /// </summary>
    public partial class CallWindow : Window
    {
        private readonly UserInfo contact;
        private readonly DispatcherTimer timer;
        private DateTime connectedAt;

        private CallState state;

        private Button btnAccept;
        private Button btnMicrophone;
        private Button btnCamera;
        private Button btnScreenShare;
        private Button btnHangUp;

        private bool microphoneMuted;

        /// <summary>Raised when the window is finished with, so the manager can forget it.</summary>
        public event Action<CallWindow> Finished;

        public string ContactId
        {
            get { return contact.id; }
        }

        public CallWindow(UserInfo contact, bool incoming)
        {
            this.contact = contact;

            InitializeComponent();

            BuildControls();

            txtName.Text = contact.name;

            imageAvatarFrame.Source = LoadResource.GetAvatarFrameFromStatus(
                (UserStatus)contact.status, UI.Data.Enums.AvatarSize.Big);

            ShowAvatar();

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (sender, e) => UpdateElapsed();

            SetState(incoming ? CallState.Ringing : CallState.Calling);

            Closing += (sender, e) =>
            {
                // Closing the window is the same as hanging up.
                if (state != CallState.Ended)
                {
                    HangUp();
                }
            };

            Closed += (sender, e) =>
            {
                timer.Stop();

                Action<CallWindow> handler = Finished;

                if (handler != null)
                {
                    handler(this);
                }
            };
        }


        #region Controls

        private void BuildControls()
        {
            btnAccept = AddControl("call.accept", Icons.Phone, Colors.SeaGreen, OnAccept);

            btnMicrophone = AddControl("call.microphone", Icons.Microphone, Colors.DimGray, OnMicrophone);
            btnCamera = AddControl("call.camera", Icons.Camera, Colors.DimGray, OnUnavailableMedia);
            btnScreenShare = AddControl("call.screenshare", Icons.ScreenShare, Colors.DimGray, OnUnavailableMedia);

            btnHangUp = AddControl("call.hangup", Icons.HangUp, Color.FromRgb(0xC6, 0x3B, 0x36), OnHangUp);

            // Nothing carries media yet, so these say so instead of looking usable.
            MarkUnavailable(btnCamera, "call.camera.unavailable");
            MarkUnavailable(btnScreenShare, "call.screenshare.unavailable");
            MarkUnavailable(btnMicrophone, "call.microphone.unavailable");
        }

        private Button AddControl(string tooltipKey, string iconData, Color background, Action action)
        {
            Path icon = new Path
            {
                Data = Geometry.Parse(iconData),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button button = new Button
            {
                Width = 52,
                Height = 52,
                Background = new SolidColorBrush(background),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(26),
                Content = icon,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(button, Language.Get(tooltipKey));

            button.Click += (sender, e) => action();

            controlBar.Children.Add(button);

            return button;
        }

        /// <summary>Dims a control and says why it does nothing yet.</summary>
        private static void MarkUnavailable(Button button, string tooltipKey)
        {
            button.Opacity = 0.45;

            ToolTip.SetTip(button, Language.Get(tooltipKey));
        }

        private void ShowAvatar()
        {
            imageAvatar.Source = LoadResource.GetDefaultAvatarImage();

            if (string.IsNullOrWhiteSpace(contact.avatar))
            {
                return;
            }

            IImage cached = AvatarCache.Get(contact.avatar, loaded => imageAvatar.Source = loaded);

            if (cached != null)
            {
                imageAvatar.Source = cached;
            }
        }

        #endregion

        #region State

        /// <summary>Moves the call to a new stage and brings the window in line with it.</summary>
        public void SetState(CallState value)
        {
            state = value;

            switch (state)
            {
                case CallState.Calling:
                    Title = Language.Format("call.title.outgoing", contact.name);
                    txtState.Text = Language.Get("call.state.calling");
                    btnAccept.IsVisible = false;
                    break;

                case CallState.Ringing:
                    Title = Language.Format("call.title.incoming", contact.name);
                    txtState.Text = Language.Get("call.state.ringing");
                    btnAccept.IsVisible = true;
                    break;

                case CallState.Connected:
                    Title = Language.Format("call.title.connected", contact.name);
                    btnAccept.IsVisible = false;
                    connectedAt = DateTime.UtcNow;
                    UpdateElapsed();
                    timer.Start();
                    break;

                case CallState.Ended:
                    timer.Stop();
                    btnAccept.IsVisible = false;
                    break;
            }

            txtSelfView.Text = Language.Get("call.selfview.off");
        }

        /// <summary>Shows how long the call has been connected, as a call window does.</summary>
        private void UpdateElapsed()
        {
            TimeSpan elapsed = DateTime.UtcNow - connectedAt;

            txtState.Text = elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"m\:ss");
        }

        /// <summary>Shows why a call finished, then closes shortly after.</summary>
        public void ShowEnded(string reasonKey)
        {
            SetState(CallState.Ended);

            txtState.Text = Language.Get(reasonKey);

            DispatcherTimer.RunOnce(() =>
            {
                try
                {
                    Close();
                }
                catch
                {
                    // Already closed by the user.
                }
            }, TimeSpan.FromSeconds(2.5));
        }

        #endregion

        #region Buttons

        private void OnAccept()
        {
            CallManager.Accept(contact.id);
        }

        private void OnHangUp()
        {
            HangUp();

            Close();
        }

        private void HangUp()
        {
            CallState previous = state;

            SetState(CallState.Ended);

            CallManager.LocalHangUp(contact.id, previous);
        }

        private void OnMicrophone()
        {
            microphoneMuted = !microphoneMuted;

            btnMicrophone.Background = new SolidColorBrush(
                microphoneMuted ? Color.FromRgb(0xC6, 0x3B, 0x36) : Colors.DimGray);
        }

        private void OnUnavailableMedia()
        {
            Compat.MessageBox.Show(Language.Get("call.media.unavailable.text"),
                Language.Get("call.media.unavailable.title"),
                Compat.MessageBoxButton.OK, Compat.MessageBoxImage.Information);
        }

        #endregion

        /// <summary>Icon outlines for the call controls, drawn rather than loaded as bitmaps.</summary>
        private static class Icons
        {
            public const string Phone =
                "M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 " +
                "3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 " +
                ".45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z";

            public const string HangUp =
                "M12 9c-1.6 0-3.15.25-4.6.72v3.1c0 .39-.23.74-.56.9-.98.49-1.87 1.12-2.66 1.85-.18.18-.43.28-.7.28" +
                "-.28 0-.53-.11-.71-.29L.29 13.08c-.18-.17-.29-.42-.29-.7 0-.28.11-.53.29-.71C3.34 8.78 7.46 7 12 " +
                "7s8.66 1.78 11.71 4.67c.18.18.29.43.29.71 0 .28-.11.53-.29.71l-2.48 2.48c-.18.18-.43.29-.71.29" +
                "-.27 0-.52-.11-.7-.28-.79-.74-1.69-1.36-2.67-1.85-.33-.16-.56-.5-.56-.9v-3.1C15.15 9.25 13.6 9 12 9z";

            public const string Microphone =
                "M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3z M17 11c0 2.76-2.24 " +
                "5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z";

            public const string Camera =
                "M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z";

            public const string ScreenShare =
                "M20 18c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z";
        }
    }
}
