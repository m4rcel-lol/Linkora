using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// A collapsible heading above a group of contacts, such as Favourites, showing how many of
    /// its contacts are online.
    /// </summary>
    class ContactGroupHeader : Border
    {
        private readonly TextBlock arrow;
        private readonly TextBlock label;

        private bool isExpanded = true;

        /// <summary>Raised when the user collapses or expands the group.</summary>
        public event Action Toggled;

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                isExpanded = value;
                arrow.Text = isExpanded ? "▼" : "▶";
            }
        }

        public ContactGroupHeader()
        {
            Background = Brushes.Transparent;
            Cursor = new Cursor(StandardCursorType.Hand);
            Padding = new Thickness(4, 3, 4, 3);
            Margin = new Thickness(0, 2, 0, 1);
            HorizontalAlignment = HorizontalAlignment.Stretch;

            arrow = new TextBlock
            {
                Text = "▼",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x7A, 0x9A)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };

            label = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x35, 0x5A, 0x88)),
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(arrow);
            panel.Children.Add(label);

            Child = panel;

            PointerPressed += (sender, e) =>
            {
                IsExpanded = !IsExpanded;

                Action handler = Toggled;

                if (handler != null)
                {
                    handler();
                }

                e.Handled = true;
            };
        }

        /// <summary>Repaints the heading for the current theme.</summary>
        public void ApplyTheme()
        {
            arrow.Foreground = Config.Theme.Accent;
            label.Foreground = Config.Theme.Accent;
        }

        public void SetText(string text)
        {
            label.Text = text;
        }
    }
}
