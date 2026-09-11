using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using WLMClient.Compat;

namespace WLMClient.UI.Controls.Dialogs
{
    /// <summary>
    /// Shared base for the three dialogs that used to be Windows Forms. They keep the original's
    /// fixed pixel layout, so each one fills a <see cref="Canvas"/> at the same coordinates the
    /// designer file used.
    /// </summary>
    public abstract class DialogWindow : Window
    {
        /// <summary>Windows Forms sizes fonts in points; Avalonia uses device independent pixels.</summary>
        protected const double PointToPixel = 96.0 / 72.0;

        protected Canvas Root { get; private set; }

        protected DialogWindow(int clientWidth, int clientHeight, string title)
        {
            Title = title;
            Width = clientWidth;
            Height = clientHeight;
            CanResize = false;
            ShowInTaskbar = false;
            Background = Config.Theme.DialogBackground;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.Manual;

            Root = new Canvas
            {
                Width = clientWidth,
                Height = clientHeight,
                Background = Config.Theme.DialogBackground
            };

            Content = Root;
        }

        protected static void Place(Control control, double left, double top)
        {
            Canvas.SetLeft(control, left);
            Canvas.SetTop(control, top);
        }

        protected void Add(Control control, double left, double top)
        {
            Place(control, left, top);
            Root.Children.Add(control);
        }

        /// <summary>Recreates the flat group box the Windows Forms designer drew.</summary>
        protected void AddGroupBox(string header, double left, double top, double width, double height)
        {
            Border box = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = Config.Theme.Separator,
                BorderThickness = new Thickness(1)
            };

            Add(box, left, top);

            TextBlock headerText = new TextBlock
            {
                Text = header,
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 12 * PointToPixel,
                FontWeight = FontWeight.Bold,
                Foreground = Config.Theme.TextPrimary,

                // Painted over the box's outline so the heading appears to break it.
                Background = Config.Theme.DialogBackground,
                Padding = new Thickness(4, 0, 4, 0)
            };

            Add(headerText, left + 6, top - 11);
        }

        protected static TextBlock CreateLabel(string text, string fontFamily, double pointSize, IBrush foreground)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(fontFamily),
                FontSize = pointSize * PointToPixel,
                Foreground = foreground ?? Config.Theme.TextPrimary
            };
        }

        /// <summary>
        /// Fits a label into a fixed slot. English keeps its designed size; a longer translation
        /// is stepped down a little until it fits, and only then shortened with an ellipsis.
        /// </summary>
        protected static void CapWidth(TextBlock label, double maxWidth)
        {
            const double MinimumFontSize = 11;

            while (label.FontSize > MinimumFontSize &&
                   MeasureWidth(label.Text, label.FontFamily, label.FontSize) > maxWidth)
            {
                label.FontSize -= 0.5;
            }

            label.MaxWidth = maxWidth;
            label.TextTrimming = TextTrimming.CharacterEllipsis;

            ToolTip.SetTip(label, label.Text);
        }

        private static double MeasureWidth(string text, FontFamily fontFamily, double fontSize)
        {
            FormattedText measured = new FormattedText(
                text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily),
                fontSize,
                Brushes.Black);

            return measured.Width;
        }

        protected static Button CreateButton(string text, double width, double height, IBrush background, IBrush foreground)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Background = background,
                Foreground = foreground ?? Config.Theme.TextPrimary,
                BorderBrush = Config.Theme.Separator,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 9.75 * PointToPixel,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Shows the dialog modally without blocking the caller. WPF's blocking ShowDialog has no
        /// safe equivalent on the UI thread here, and no caller used the return value.
        /// </summary>
        public void ShowDialog(Action closed)
        {
            if (closed != null)
            {
                Closed += (s, e) => closed();
            }

            Window owner = MessageBox.GetActiveWindow();

            if (owner != null && owner.IsVisible && !ReferenceEquals(owner, this))
            {
                ShowDialog(owner);
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Show();
            }
        }
    }
}
