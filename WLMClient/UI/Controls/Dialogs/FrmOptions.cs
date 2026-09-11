using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using WLMClient.Compat;
using WLMClient.Config;
using WLMClient.Layout;
using WLMClient.Locale;
using WLMClient.Network;

namespace WLMClient.UI.Controls.Dialogs
{
    /// <summary>The options dialog: display name and avatar, laid out as the designer file had it.</summary>
    public class FrmOptions : DialogWindow
    {
        public Bitmap _currentBitmap;
        public string newImage = "";

        private TextBox txtName;
        private Image imgAvatar;
        private Button btnBrowse;
        private Button btnSave;
        private Button btnCancel;
        private ComboBox cmbLanguage;
        private TextBox txtUsername;
        private CheckBox chkDarkTheme;
        private TextBlock avatarHeading;
        private TextBlock usernameHint;

        public FrmOptions() : base(440, 418, Language.Get("options.title"))
        {
            InitializeComponent();

            txtName.Text = Personal.USER_INFO.name;
            txtUsername.Text = Personal.USER_INFO.id;
        }

        private void InitializeComponent()
        {
            AddGroupBox(Language.Get("options.title"), 15, 16, 410, 384);

            // Left column: the picture, framed the way it appears elsewhere, with its button
            // directly beneath. Right column: the fields, each under its own heading.
            avatarHeading = CreateLabel(Language.Get("options.newavatar"),
                "Segoe UI, Helvetica, Arial", 14.25, Config.Theme.Accent);
            CapWidth(avatarHeading, 140);
            Add(avatarHeading, 34, 42);

            AddAvatarPreview(34, 72);

            btnBrowse = CreateStrongButton(Language.Get("dialog.browse"), 140, 28);
            btnBrowse.FontSize = 11 * PointToPixel;
            btnBrowse.Click += btnBrowse_Click;
            Add(btnBrowse, 34, 210);

            TextBlock nameHeading = CreateLabel(Language.Get("options.displayname"),
                "Segoe UI, Helvetica, Arial", 14.25);
            CapWidth(nameHeading, 220);
            Add(nameHeading, 196, 42);

            txtName = new TextBox
            {
                Width = 220,
                Height = 28,
                MaxLength = 22,
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 12 * PointToPixel,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };

            Add(txtName, 196, 72);

            TextBlock usernameHeading = CreateLabel(Language.Get("options.username"),
                "Segoe UI, Helvetica, Arial", 14.25);
            CapWidth(usernameHeading, 220);
            Add(usernameHeading, 196, 116);

            txtUsername = new TextBox
            {
                Width = 220,
                Height = 28,
                MaxLength = 29,
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 12 * PointToPixel,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };

            ToolTip.SetTip(txtUsername, Language.Get("options.username.hint"));

            Add(txtUsername, 196, 146);

            usernameHint = CreateLabel(Language.Get("options.username.hint"),
                "Segoe UI, Helvetica, Arial", 8.5, Config.Theme.TextSecondary);
            usernameHint.TextWrapping = TextWrapping.Wrap;
            usernameHint.MaxWidth = 220;
            Add(usernameHint, 196, 178);

            TextBlock languageHeading = CreateLabel(Language.Get("options.language"),
                "Segoe UI, Helvetica, Arial", 14.25);
            CapWidth(languageHeading, 220);
            Add(languageHeading, 196, 212);

            AddLanguagePicker(196, 242);

            AddThemeToggle(196, 288);

            btnCancel = CreateButton(Language.Get("dialog.close"), 105, 30);
            btnCancel.Click += btnCancel_Click;
            Add(btnCancel, 196, 328);

            btnSave = CreateButton(Language.Get("dialog.save"), 105, 30);
            btnSave.Click += btnSave_Click;
            btnSave.IsDefault = true;
            Add(btnSave, 311, 328);
        }

        /// <summary>
        /// Shows the picture currently in use, inside the same frame the rest of the application
        /// draws around a profile picture. Previously this stayed blank until a new file was picked.
        /// </summary>
        private void AddAvatarPreview(double left, double top)
        {
            imgAvatar = new Image
            {
                Stretch = Stretch.Fill,
                Width = Resource.Images.Attributes.AVATAR_CHAT_SIZE_WIDTH,
                Height = Resource.Images.Attributes.AVATAR_CHAT_SIZE_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Image frame = new Image
            {
                Stretch = Stretch.None,
                Source = LoadResource.GetAvatarFrameFromStatus(
                    Personal.USER_INFO == null
                        ? WLMData.Enums.UserStatus.Offline
                        : (WLMData.Enums.UserStatus)Personal.USER_INFO.status,
                    UI.Data.Enums.AvatarSize.Big)
            };

            Grid framed = new Grid
            {
                Width = Resource.Images.Attributes.AVATAR_FRAME_WIDTH,
                Height = Resource.Images.Attributes.AVATAR_FRAME_HEIGHT
            };

            framed.Children.Add(imgAvatar);
            framed.Children.Add(frame);

            Viewbox box = new Viewbox
            {
                Width = 140,
                Stretch = Stretch.Uniform,
                Child = framed
            };

            Add(box, left, top);

            ShowCurrentAvatar();
        }

        /// <summary>Loads the picture the account is currently using into the preview.</summary>
        private void ShowCurrentAvatar()
        {
            imgAvatar.Source = LoadResource.GetDefaultAvatarImage();

            string url = Personal.USER_INFO == null ? "" : Personal.USER_INFO.avatar;

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            IImage cached = AvatarCache.Get(url, loaded => imgAvatar.Source = loaded);

            if (cached != null)
            {
                imgAvatar.Source = cached;
            }
        }

        /// <summary>
        /// The language list. Entries come from the Languages folder, so a language added there
        /// shows up here without rebuilding.
        /// </summary>
        private void AddLanguagePicker(double left, double top)
        {
            List<LanguageInfo> languages = Language.GetAvailable();

            cmbLanguage = new ComboBox
            {
                Width = 220,
                Height = 28,
                ItemsSource = languages,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontFamily = new FontFamily("Segoe UI, Helvetica, Arial"),
                FontSize = 12 * PointToPixel
            };

            cmbLanguage.SelectedIndex = Math.Max(0, languages.FindIndex(
                x => string.Equals(x.Code, Language.CurrentCode, StringComparison.OrdinalIgnoreCase)));

            cmbLanguage.SelectionChanged += LanguageChanged;

            Add(cmbLanguage, left, top);
        }

        /// <summary>Repaints the pieces of this dialog that have colours of their own.</summary>
        protected override void ApplyTheme()
        {
            base.ApplyTheme();

            if (avatarHeading != null)
            {
                avatarHeading.Foreground = Config.Theme.Accent;
            }

            if (usernameHint != null)
            {
                usernameHint.Foreground = Config.Theme.TextSecondary;
            }

            if (chkDarkTheme != null)
            {
                chkDarkTheme.Foreground = Config.Theme.TextPrimary;
            }
        }

        /// <summary>The dark theme switch. Applies at once so the effect is visible while choosing.</summary>
        private void AddThemeToggle(double left, double top)
        {
            chkDarkTheme = new CheckBox
            {
                Content = Language.Get("options.darktheme"),
                IsChecked = Config.Theme.IsDark,
                FontFamily = new FontFamily("Segoe UI, Helvetica, Arial"),
                FontSize = 11 * PointToPixel,
                Foreground = Config.Theme.TextPrimary
            };

            chkDarkTheme.IsCheckedChanged += ThemeChanged;

            Add(chkDarkTheme, left, top);
        }

        private void ThemeChanged(object sender, RoutedEventArgs e)
        {
            bool dark = chkDarkTheme.IsChecked == true;

            if (dark == Config.Theme.IsDark)
            {
                return;
            }

            SaveData configuration = SaveDataManager.GetConfiguration();
            configuration.darkTheme = dark;

            SaveDataManager.SaveConfiguration(configuration);

            // Applies to every open window immediately; this dialog keeps the colours it was
            // built with until it is reopened.
            Config.Theme.Load(dark);
        }

        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            LanguageInfo selected = cmbLanguage.SelectedItem as LanguageInfo;

            if (selected == null || string.Equals(selected.Code, Language.CurrentCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SaveData configuration = SaveDataManager.GetConfiguration();
            configuration.language = selected.Code;

            SaveDataManager.SaveConfiguration(configuration);

            // Applies to every open window immediately; this dialog keeps its current wording
            // until it is reopened.
            Language.Load(selected.Code);
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (Config.Properties.AVATAR_IMAGE_UPLOAD_URL == "")
            {
                MessageBox.Show(Language.Get("options.avatar.disabled.text"), Language.Get("options.avatar.disabled.title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Language.Get("options.newavatar"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.jpe", "*.jfif", "*.png" }
                    }
                }
            });

            if (files == null || files.Count == 0)
            {
                return;
            }

            string localPath = files[0].TryGetLocalPath();
            if (localPath == null)
            {
                return;
            }

            Random rndm = new Random();
            string name = rndm.Next(1000000, 99999999).ToString() + ".png";
            string temporaryPath = Path.Combine(Path.GetTempPath(), name);

            try
            {
                using (Bitmap source = new Bitmap(localPath))
                {
                    _currentBitmap = imgResize(source, 100, 100);
                }

                _currentBitmap.Save(temporaryPath);

                string uploadValue = await upload(temporaryPath);

                if (uploadValue == "0")
                {
                    MessageBox.Show(Language.Get("options.avatar.failed"), Language.Get("options.error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    newImage = uploadValue;
                }

                imgAvatar.Source = _currentBitmap;
            }
            catch (Exception err)
            {
                MessageBox.Show(Language.Format("options.avatar.error", err.ToString()),
                    Language.Get("options.error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // Nothing useful to do if the temporary file cannot be removed.
                }
            }
        }

        /// <summary>
        /// Posts the image to the upload page. The field name has to stay "file" because that is
        /// what upload.php reads and what WebClient.UploadFile used to send.
        /// </summary>
        public static async Task<string> upload(string file)
        {
            string returnValue = "0";

            string url = Config.Properties.AVATAR_IMAGE_UPLOAD_URL;

            using (HttpClient client = new HttpClient())
            using (MultipartFormDataContent content = new MultipartFormDataContent())
            {
                client.Timeout = TimeSpan.FromSeconds(60);

                ByteArrayContent fileContent = new ByteArrayContent(File.ReadAllBytes(file));
                content.Add(fileContent, "file", Path.GetFileName(file));

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseAsString = await response.Content.ReadAsStringAsync();

                if (responseAsString.Trim() != "0")
                {
                    returnValue = responseAsString.Trim();
                }
            }

            return returnValue;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (newImage != "")
            {
                Personal.USER_INFO.avatar = newImage;
            }

            Personal.USER_INFO.name = txtName.Text ?? "";

            Client.SendUserUpdate();

            // The sign in name is the account's identity, so the server has to agree to the change
            // and answers separately; everything else here applies immediately.
            string requestedUsername = (txtUsername.Text ?? "").Trim();

            if (requestedUsername.Length > 0 &&
                !string.Equals(requestedUsername, Personal.USER_INFO.id, StringComparison.OrdinalIgnoreCase))
            {
                Client.RequestUsernameChange(requestedUsername);
            }

            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>Scales an image to the requested size with smooth filtering.</summary>
        public static Bitmap imgResize(Bitmap source, int newWidth, int newHeight)
        {
            if (newWidth == 0 || newHeight == 0)
            {
                return source;
            }

            return source.CreateScaledBitmap(new PixelSize(newWidth, newHeight),
                BitmapInterpolationMode.HighQuality);
        }
    }
}
