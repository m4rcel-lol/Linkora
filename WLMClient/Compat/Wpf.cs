using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace WLMClient.Compat
{
    /// <summary>Named font weights, matching the WPF class the original code used.</summary>
    public static class FontWeights
    {
        public static FontWeight Normal { get { return FontWeight.Normal; } }
        public static FontWeight Regular { get { return FontWeight.Regular; } }
        public static FontWeight Bold { get { return FontWeight.Bold; } }
        public static FontWeight SemiBold { get { return FontWeight.SemiBold; } }
    }

    /// <summary>Parses colour strings such as "#FCFCFC" into brushes, like WPF's BrushConverter.</summary>
    public class BrushConverter
    {
        public IBrush ConvertFrom(object value)
        {
            string text = value as string;

            if (string.IsNullOrEmpty(text))
            {
                return Brushes.Transparent;
            }

            try
            {
                return Brush.Parse(text);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public IBrush ConvertFromString(string value)
        {
            return ConvertFrom(value);
        }
    }

    /// <summary>Brush helpers for shapes WPF offered through constructors Avalonia does not have.</summary>
    public static class BrushHelper
    {
        /// <summary>A top to bottom two stop gradient, the shape used for contact row highlights.</summary>
        public static LinearGradientBrush VerticalGradient(Color from, Color to)
        {
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative)
            };

            brush.GradientStops.Add(new GradientStop(from, 0));
            brush.GradientStops.Add(new GradientStop(to, 1));

            return brush;
        }
    }

    public enum MessageBoxButton
    {
        OK = 0,
        OKCancel = 1,
        YesNo = 4
    }

    public enum MessageBoxImage
    {
        None = 0,
        Error = 16,
        Question = 32,
        Warning = 48,
        Information = 64
    }

    public enum MessageBoxResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 6,
        No = 7
    }

    /// <summary>
    /// A drop in replacement for WPF's MessageBox. The application never inspects the result, so
    /// this shows the dialog without blocking the UI thread (which a modal wait would deadlock).
    /// </summary>
    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Show(messageBoxText, caption, button, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button,
            MessageBoxImage icon)
        {
            Dispatcher.UIThread.Post(() => ShowWindow(messageBoxText, caption, icon, null));

            return MessageBoxResult.OK;
        }

        /// <summary>Shows the dialog and runs <paramref name="closed"/> once it is dismissed.</summary>
        public static void ShowThen(string messageBoxText, string caption, MessageBoxImage icon, Action closed)
        {
            Dispatcher.UIThread.Post(() => ShowWindow(messageBoxText, caption, icon, closed));
        }

        private static void ShowWindow(string messageBoxText, string caption, MessageBoxImage icon, Action closed)
        {
            Window dialog = new Window
            {
                Title = string.IsNullOrEmpty(caption) ? " " : caption,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White,
                MinWidth = 320
            };

            TextBlock glyph = new TextBlock
            {
                Text = GetGlyph(icon),
                FontSize = 28,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = GetGlyphBrush(icon)
            };

            TextBlock message = new TextBlock
            {
                Text = messageBoxText ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsDefault = true
            };

            okButton.Click += (s, e) => dialog.Close();

            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(glyph);
            content.Children.Add(message);

            StackPanel root = new StackPanel { Margin = new Thickness(18), Spacing = 16 };
            root.Children.Add(content);
            root.Children.Add(okButton);

            dialog.Content = root;

            if (closed != null)
            {
                dialog.Closed += (s, e) => closed();
            }

            Window owner = GetActiveWindow();

            if (owner != null && owner.IsVisible && !ReferenceEquals(owner, dialog))
            {
                dialog.ShowDialog(owner);
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.Show();
            }
        }

        private static string GetGlyph(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error:
                    return "⛔";
                case MessageBoxImage.Warning:
                    return "⚠";
                case MessageBoxImage.Question:
                    return "?";
                case MessageBoxImage.Information:
                    return "ℹ";
                default:
                    return string.Empty;
            }
        }

        private static IBrush GetGlyphBrush(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error:
                    return Brushes.Firebrick;
                case MessageBoxImage.Warning:
                    return Brushes.Goldenrod;
                default:
                    return Brushes.SteelBlue;
            }
        }

        /// <summary>The window a dialog should be parented to, preferring the focused one.</summary>
        public static Window GetActiveWindow()
        {
            IClassicDesktopStyleApplicationLifetime lifetime =
                Application.Current == null
                    ? null
                    : Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

            if (lifetime == null)
            {
                return null;
            }

            foreach (Window window in lifetime.Windows)
            {
                if (window.IsActive)
                {
                    return window;
                }
            }

            return lifetime.MainWindow;
        }
    }
}
