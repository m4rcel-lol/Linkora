using System;
using System.Collections.Generic;

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

        /// <summary>Labels that follow the theme's primary text colour.</summary>
        private readonly List<TextBlock> themedLabels = new List<TextBlock>();

        /// <summary>Buttons whose colours came from the theme rather than being chosen outright.</summary>
        private readonly List<Button> themedButtons = new List<Button>();
        private readonly List<Button> strongButtons = new List<Button>();

        private readonly List<Border> themedBoxes = new List<Border>();

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

            Config.Theme.Changed += ApplyTheme;
            Closed += (sender, e) => Config.Theme.Changed -= ApplyTheme;
        }

        /// <summary>
        /// Repaints the dialog for the current theme. Without this a dialog left open while the
        /// theme is switched keeps the colours it was built with, which in practice meant dark
        /// text on a dark background.
        /// </summary>
        protected virtual void ApplyTheme()
        {
            Background = Config.Theme.DialogBackground;
            Root.Background = Config.Theme.DialogBackground;

            foreach (TextBlock label in themedLabels)
            {
                label.Foreground = Config.Theme.TextPrimary;
                label.Background = Config.Theme.DialogBackground;
            }

            foreach (Border box in themedBoxes)
            {
                box.BorderBrush = Config.Theme.Separator;
            }

            foreach (Button button in themedButtons)
            {
                button.BorderBrush = Config.Theme.Separator;
                button.Background = Config.Theme.ButtonBackground;
                button.Foreground = Config.Theme.TextPrimary;
            }

            foreach (Button button in strongButtons)
            {
                button.BorderBrush = Config.Theme.Separator;
                button.Background = Config.Theme.ButtonStrongBackground;
                button.Foreground = Config.Theme.ButtonStrongForeground;
            }
        }

        /// <summary>Registers a label so it follows the theme's text colour.</summary>
        protected void TrackLabel(TextBlock label)
        {
            themedLabels.Add(label);
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

            themedBoxes.Add(box);

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

            themedLabels.Add(headerText);

            Add(headerText, left + 6, top - 11);
        }

        /// <summary>
        /// A label in the theme's own text colour, which is kept up to date when the theme changes.
        /// Use the overload taking a brush for text that has a colour of its own.
        /// </summary>
        protected TextBlock CreateLabel(string text, string fontFamily, double pointSize)
        {
            TextBlock label = CreateLabel(text, fontFamily, pointSize, Config.Theme.TextPrimary);

            TrackLabel(label);

            return label;
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

        /// <summary>An ordinary dialog button, following the theme.</summary>
        protected Button CreateButton(string text, double width, double height)
        {
            Button button = BuildButton(text, width, height,
                Config.Theme.ButtonBackground, Config.Theme.TextPrimary);

            themedButtons.Add(button);

            return button;
        }

        /// <summary>The emphasised button the designer file drew in black.</summary>
        protected Button CreateStrongButton(string text, double width, double height)
        {
            Button button = BuildButton(text, width, height,
                Config.Theme.ButtonStrongBackground, Config.Theme.ButtonStrongForeground);

            strongButtons.Add(button);

            return button;
        }

        private static Button BuildButton(string text, double width, double height, IBrush background, IBrush foreground)
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
