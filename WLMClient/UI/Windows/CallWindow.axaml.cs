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
        private Button btnHangUp;

        private bool microphoneMuted;

        /// <summary>Kept so the call timer does not overwrite a missing device warning.</summary>
        private string audioNotice;

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
            btnAccept = AddControl("call.accept", Icons.Phone, Color.FromRgb(0x3D, 0xA5, 0x5C), OnAccept);
            btnMicrophone = AddControl("call.microphone", Icons.Microphone, Color.FromRgb(0x3A, 0x3F, 0x48), OnMicrophone);
            btnHangUp = AddControl("call.hangup", Icons.HangUp, Color.FromRgb(0xC6, 0x3B, 0x36), OnHangUp);
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

        }

        /// <summary>Shows how long the call has been connected, as a call window does.</summary>
        private void UpdateElapsed()
        {
            TimeSpan elapsed = DateTime.UtcNow - connectedAt;

            string time = elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"m\:ss");

            txtState.Text = string.IsNullOrEmpty(audioNotice) ? time : time + "  ·  " + audioNotice;
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

            CallManager.SetMuted(microphoneMuted);

            btnMicrophone.Background = new SolidColorBrush(microphoneMuted
                ? Color.FromRgb(0xC6, 0x3B, 0x36)
                : Color.FromRgb(0x3A, 0x3F, 0x48));

            ToolTip.SetTip(btnMicrophone,
                Language.Get(microphoneMuted ? "call.unmute" : "call.microphone"));
        }

        /// <summary>
        /// Says so when a call connected without a working microphone or output, rather than
        /// leaving the user wondering why nobody can hear them.
        /// </summary>
        public void ShowAudioState(bool hasMicrophone, bool hasSpeakers)
        {
            if (hasMicrophone && hasSpeakers)
            {
                return;
            }

            string key = !hasMicrophone && !hasSpeakers
                ? "call.audio.none"
                : (!hasMicrophone ? "call.audio.nomicrophone" : "call.audio.nospeakers");

            audioNotice = Language.Get(key);
            txtState.Text = audioNotice;

            btnMicrophone.Opacity = hasMicrophone ? 1 : 0.45;
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


        }
    }
}
