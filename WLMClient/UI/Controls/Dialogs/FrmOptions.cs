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

        public FrmOptions() : base(410, 310, Language.Get("options.title"))
        {
            InitializeComponent();

            txtName.Text = Personal.USER_INFO.name;
        }

        private void InitializeComponent()
        {
            AddGroupBox(Language.Get("options.title"), 15, 16, 380, 275);

            // Headings sit above their fields and are capped to the field width, so a longer
            // translation shortens with an ellipsis instead of running off the dialog.
            TextBlock label2 = CreateLabel(Language.Get("options.newavatar"), "Segoe UI, Helvetica, Arial", 14.25,
                new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)));
            CapWidth(label2, 112);
            Add(label2, 31, 48);

            TextBlock label3 = CreateLabel(Language.Get("options.displayname"), "Segoe UI, Helvetica, Arial", 14.25, Brushes.Black);
            CapWidth(label3, 223);
            Add(label3, 149, 48);

            Border avatarBorder = new Border
            {
                Width = 98,
                Height = 98,
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x82, 0x82, 0x82))
            };

            imgAvatar = new Image { Stretch = Stretch.Uniform };
            avatarBorder.Child = imgAvatar;

            Add(avatarBorder, 36, 84);

            txtName = new TextBox
            {
                Width = 223,
                Height = 26,
                MaxLength = 22,
                FontFamily = new FontFamily("Microsoft Sans Serif, Helvetica, Arial"),
                FontSize = 12 * PointToPixel,
                TextAlignment = TextAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Add(txtName, 149, 84);

            btnBrowse = CreateButton(Language.Get("dialog.browse"), 98, 30, Brushes.Black, Brushes.White);
            btnBrowse.FontSize = 12 * PointToPixel;
            btnBrowse.Click += btnBrowse_Click;
            Add(btnBrowse, 36, 196);

            btnSave = CreateButton(Language.Get("dialog.save"), 94, 51, Brushes.WhiteSmoke, Brushes.Black);
            btnSave.Click += btnSave_Click;
            Add(btnSave, 278, 175);

            btnCancel = CreateButton(Language.Get("dialog.close"), 94, 51, Brushes.WhiteSmoke, Brushes.Black);
            btnCancel.Click += btnCancel_Click;
            Add(btnCancel, 149, 175);

            AddLanguagePicker();
        }

        /// <summary>
        /// The language list. Entries come from the Languages folder, so a language added there
        /// shows up here without rebuilding.
        /// </summary>
        private void AddLanguagePicker()
        {
            TextBlock label = CreateLabel(Language.Get("options.language"), "Segoe UI, Helvetica, Arial", 14.25,
                Brushes.Black);
            CapWidth(label, 112);
            Add(label, 31, 238);

            List<LanguageInfo> languages = Language.GetAvailable();

            cmbLanguage = new ComboBox
            {
                Width = 223,
                Height = 26,
                ItemsSource = languages,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontFamily = new FontFamily("Segoe UI, Helvetica, Arial"),
                FontSize = 12 * PointToPixel
            };

            cmbLanguage.SelectedIndex = Math.Max(0, languages.FindIndex(
                x => string.Equals(x.Code, Language.CurrentCode, StringComparison.OrdinalIgnoreCase)));

            cmbLanguage.SelectionChanged += LanguageChanged;

            Add(cmbLanguage, 149, 238);
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
