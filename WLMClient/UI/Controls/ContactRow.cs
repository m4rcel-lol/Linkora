using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using WLMClient.Layout;
using WLMClient.Locale;
using WLMData.Data.Packets;
using WLMData.Enums;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// One row of the contact list: the contact's profile picture framed in their status colour,
    /// their name, and underneath either their personal message or the latest message exchanged
    /// with them.
    /// </summary>
    class ContactRow : Border
    {
        public const double RowHeight = 42;

        private readonly Image avatar;
        private readonly Border avatarFrame;
        private readonly TextBlock nameBlock;
        private readonly TextBlock secondBlock;

        private string avatarUrl;

        /// <summary>The contact this row belongs to.</summary>
        public string ContactId { get; private set; }

        public Image AvatarImage { get { return avatar; } }
        public TextBlock NameBlock { get { return nameBlock; } }
        public TextBlock SecondBlock { get { return secondBlock; } }

        public ContactRow(string contactId)
        {
            ContactId = contactId;
            Tag = contactId;

            Height = RowHeight;
            Margin = new Thickness(0, 0, 0, 2);
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Cursor = new Cursor(StandardCursorType.Arrow);

            avatar = new Image
            {
                Stretch = Stretch.UniformToFill,
                Width = 28,
                Height = 28
            };

            RenderOptions.SetBitmapInterpolationMode(avatar, BitmapInterpolationMode.HighQuality);

            avatarFrame = new Border
            {
                Width = 32,
                Height = 32,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                Child = avatar,
                ClipToBounds = true
            };

            nameBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                FontWeight = FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            secondBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontWeight = FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };

            StackPanel text = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            text.Children.Add(nameBlock);
            text.Children.Add(secondBlock);

            Grid layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetColumn(avatarFrame, 0);
            Grid.SetColumn(text, 1);

            layout.Children.Add(avatarFrame);
            layout.Children.Add(text);

            Child = layout;
        }

        /// <summary>The colour that frames the picture, showing the contact's presence.</summary>
        public static IBrush GetStatusBrush(UserStatus status)
        {
            switch (status)
            {
                case UserStatus.Available:
                    return new SolidColorBrush(Color.FromRgb(0x6C, 0xB0, 0x33));
                case UserStatus.Busy:
                    return new SolidColorBrush(Color.FromRgb(0xC6, 0x3B, 0x36));
                case UserStatus.Away:
                    return new SolidColorBrush(Color.FromRgb(0xE8, 0x9B, 0x1E));
                default:
                    return new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xB4));
            }
        }

        /// <summary>Applies a contact's details to the row.</summary>
        public void Update(UserInfo contact)
        {
            UserStatus status = (UserStatus)contact.status;

            avatarFrame.BorderBrush = GetStatusBrush(status);

            // Offline contacts are dimmed, the way the original list greyed them out.
            avatar.Opacity = status == UserStatus.Offline ? 0.55 : 1;

            nameBlock.Text = contact.name;
            Data.TextParser.ParseText(nameBlock, false);

            ToolTip.SetTip(this, contact.id + " (" + Language.GetStatus(status) + ")");

            SetAvatar(contact.avatar);
            RefreshSecondLine(contact);
        }

        /// <summary>
        /// The second line shows the contact's personal message, falling back to the most recent
        /// message in the conversation so the row still says something useful.
        /// </summary>
        public void RefreshSecondLine(UserInfo contact)
        {
            string comment = contact == null ? "" : (contact.comment ?? "");

            if (comment.Trim().Length > 0)
            {
                secondBlock.Text = comment;
                secondBlock.FontStyle = FontStyle.Normal;

                Data.TextParser.ParseText(secondBlock, true);

                return;
            }

            string summary = Conversations.GetSummary(ContactId);

            secondBlock.Text = summary ?? "";
            secondBlock.FontStyle = summary == null ? FontStyle.Normal : FontStyle.Italic;

            Data.TextParser.ParseText(secondBlock, false);
        }

        /// <summary>Shows the contact's picture, fetching it in the background if needed.</summary>
        private void SetAvatar(string url)
        {
            avatarUrl = url;

            avatar.Source = LoadResource.GetDefaultAvatarImage();

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            string requested = url;

            IImage cached = AvatarCache.Get(url, loaded =>
            {
                // The row may have been reassigned while the picture was downloading.
                if (string.Equals(avatarUrl, requested, StringComparison.Ordinal))
                {
                    avatar.Source = loaded;
                }
            });

            if (cached != null)
            {
                avatar.Source = cached;
            }
        }
    }
}
