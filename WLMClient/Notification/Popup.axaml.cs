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
using Avalonia.Platform;
using Avalonia.Threading;

using WLMClient.UI.Data;
using WLMClient.UI.Windows;

namespace WLMClient.Notification
{
    /// <summary>
    /// Interaction logic for Popup.axaml
    /// </summary>
    public partial class Popup : Window
    {
        ChatWindow window = null;

        /// <summary>Total time the popup stays on screen, matching the original storyboard.</summary>
        private static readonly TimeSpan VisibleDuration = TimeSpan.FromSeconds(9);
        private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(1);

        private DispatcherTimer timer;
        private DateTime shownAt;

        public Popup(string Line1, string Line2, ChatWindow Window)
        {
            InitializeComponent();

            notificationBorder.PointerPressed += Border_PointerPressed;

            line1.Text = Line1;
            line2.Text = Line2;
            window = Window;

            TextParser.ParseText(line1, false);
            TextParser.ParseText(line2, false);

            Opened += OnOpened;
        }


        private void OnOpened(object sender, EventArgs e)
        {
            PositionInCorner();
            StartFadeTimer();
        }

        /// <summary>Places the popup at the bottom right of the screen's working area.</summary>
        private void PositionInCorner()
        {
            Screen screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

            if (screen == null)
            {
                return;
            }

            PixelRect workingArea = screen.WorkingArea;
            double scaling = screen.Scaling;

            int width = (int)Math.Round(Bounds.Width * scaling);
            int height = (int)Math.Round(Bounds.Height * scaling);

            this.Position = new PixelPoint(
                workingArea.X + workingArea.Width - width - 5,
                workingArea.Y + workingArea.Height - height - 5);
        }

        /// <summary>
        /// Replaces the WPF storyboard: hold at full opacity, then fade out and close.
        /// </summary>
        private void StartFadeTimer()
        {
            shownAt = DateTime.UtcNow;

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (s, e) =>
            {
                TimeSpan elapsed = DateTime.UtcNow - shownAt;

                if (elapsed < VisibleDuration)
                {
                    return;
                }

                double fadeProgress = (elapsed - VisibleDuration).TotalMilliseconds / FadeDuration.TotalMilliseconds;

                if (fadeProgress >= 1)
                {
                    Storyboard_Completed(this, EventArgs.Empty);
                    return;
                }

                this.Opacity = 1 - fadeProgress;
            };

            timer.Start();
        }

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        private void Border_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

            if (properties.IsRightButtonPressed)
            {
                Border_PreviewMouseRightButtonDown(sender, e);
                return;
            }

            if (properties.IsLeftButtonPressed)
            {
                Window_PreviewMouseLeftButtonDown(sender, e);
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            StopTimer();

            if (window != null)
            {
                window.Show();
                window.Activate();

                window.WindowState = WindowState.Normal;

                this.Close();
            }
            else
            {
                this.Close();
            }
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            StopTimer();

            this.Close();
        }

        private void Border_PreviewMouseRightButtonDown(object sender, PointerPressedEventArgs e)
        {
            StopTimer();

            this.Close();
        }
    }
}
