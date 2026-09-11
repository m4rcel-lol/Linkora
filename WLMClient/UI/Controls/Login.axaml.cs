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
using Avalonia.Media.Imaging;

using WLMClient.Compat;
using WLMClient.Config;
using WLMClient.Locale;
using WLMData.Enums;
using WLMClient.UI.Data.Enums;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// Interaction logic for Login.axaml
    /// </summary>
    public partial class Login : UserControl
    {
        private UserStatus selectedUserStatus;
        private SaveData loginConfiguration;

        public Login()
        {
            InitializeComponent();

            checkAvailable.PointerPressed += checkAvailable_PreviewMouseLeftButtonDown;
            checkBusy.PointerPressed += checkBusy_PreviewMouseLeftButtonDown;
            checkAway.PointerPressed += checkAway_PreviewMouseLeftButtonDown;
            checkOffline.PointerPressed += checkOffline_PreviewMouseLeftButtonDown;

            checkRememberMe.Click += checkRememberMe_Click;
            checkRememberMyPassword.Click += checkRememberMyPassword_Click;
            checkSignInAutomatically.Click += checkSignInAutomatically_Click;

            btnLogin.Click += btnLogin_Click;
            txtPass.KeyUp += txtPass_PreviewKeyUp;
            txtSignUp.PointerPressed += txtSignUp_PointerPressed;

            ApplyLanguage();
            Language.Changed += ApplyLanguage;
            DetachedFromVisualTree += (sender, e) => Language.Changed -= ApplyLanguage;

            imagePartnerAvatar.Source = Layout.LoadResource.GetDefaultAvatarImage();
            imagePartnerFrame.Source = Layout.LoadResource.GetAvatarFrameFromStatus(UserStatus.Offline, AvatarSize.Big);

            background.Source = Layout.Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_WIDE);

            checkAvailable.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Available);
            checkBusy.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Busy);
            checkAway.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Away);
            checkOffline.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Offline);

            SetStatusSelection(UserStatus.Available);

            loginConfiguration = SaveDataManager.GetConfiguration();

            txtServer.Text = string.IsNullOrWhiteSpace(loginConfiguration.saveServer)
                ? Config.Properties.FormatServer(Config.Properties.SERVER_ADDRESS, Config.Properties.SERVER_PORT)
                : loginConfiguration.saveServer;

            if (loginConfiguration.rememberId)
            {
                txtId.Text = loginConfiguration.saveId;
                checkRememberMe.IsChecked = true;
            }

            if (loginConfiguration.rememberPassword)
            {
                txtPass.Password = loginConfiguration.savePass;
                checkRememberMyPassword.IsChecked = true;
            }

            if (loginConfiguration.autoLogin)
            {
                checkSignInAutomatically.IsChecked = true;

                // Connecting needs the control to be attached first, so defer the sign in.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btnLogin_Click(null, null),
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }


        /// <summary>Applies the current language to the sign in page.</summary>
        private void ApplyLanguage()
        {
            txtSignInTitle.Text = Language.Get("login.title");
            txtSignInPrompt.Text = Language.Get("login.prompt");
            txtSignUp.Text = Language.Get("login.signup");
            txtServer.Watermark = Language.Get("login.server.watermark");
            checkRememberMe.Content = Language.Get("login.rememberme");
            checkRememberMyPassword.Content = Language.Get("login.rememberpassword");
            checkSignInAutomatically.Content = Language.Get("login.autosignin");
            btnLogin.Content = Language.Get("login.button");

            ToolTip.SetTip(txtServer, Language.Get("login.server.tooltip"));
        }

        private void checkAvailable_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Available);
        }

        private void checkBusy_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Busy);
        }

        private void checkAway_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Away);
        }

        private void checkOffline_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Offline);
        }

        private void checkRememberMe_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.rememberId = checkValue;
        }

        private void checkRememberMyPassword_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.rememberPassword = checkValue;
        }

        private void checkSignInAutomatically_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.autoLogin = checkValue;
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string host;
            int port;

            if (!Config.Properties.TryParseServer(txtServer.Text, out host, out port))
            {
                MessageBox.Show(Language.Get("login.server.needed.text"),
                    Language.Get("login.server.needed.title"), MessageBoxButton.OK, MessageBoxImage.Information);

                txtServer.Focus();

                return;
            }

            Config.Properties.SERVER_ADDRESS = host;
            Config.Properties.SERVER_PORT = port;
            Config.Properties.SERVER_DISPLAY = Config.Properties.FormatServer(host, port);

            loginConfiguration.saveServer = txtServer.Text.Trim();

            if (checkRememberMe.IsChecked.Value & !checkRememberMyPassword.IsChecked.Value)
            {
                loginConfiguration.saveId = txtId.Text;
                loginConfiguration.savePass = "";
            }

            if (checkRememberMyPassword.IsChecked.Value)
            {
                loginConfiguration.saveId = txtId.Text;
                loginConfiguration.savePass = txtPass.Password;
            }

            SaveDataManager.SaveConfiguration(loginConfiguration);

            if (!Network.Client.Connect())
            {
                return;
            }

            Network.Client.AuthenticateUser(txtId.Text ?? "", txtPass.Password, Convert.ToInt16(selectedUserStatus));
        }


        /// <summary>Opens the registration page in the user's browser.</summary>
        private void txtSignUp_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Follows whatever server is in the box, so the link points at the right place
            // before the user has signed in anywhere.
            string url = Config.Properties.GetRegistrationUrl(txtServer.Text);

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(Language.Get("login.signup.needed.text"),
                    Language.Get("error.signup.title"), MessageBoxButton.OK, MessageBoxImage.Information);

                txtServer.Focus();

                return;
            }

            UI.Data.TextParser.OpenUrl(url);
        }

        private void txtPass_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnLogin_Click(null, null);
            }
        }

        public void WindowSizeChanged(double height)
        {
            this.Height = height;
        }

        private void SetStatusSelection(UserStatus status)
        {
            borderAvailable.BorderThickness = new Thickness(0);
            borderBusy.BorderThickness = new Thickness(0);
            borderOffline.BorderThickness = new Thickness(0);
            borderAway.BorderThickness = new Thickness(0);

            if (status == UserStatus.Available)
            {
                borderAvailable.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Busy)
            {
                borderBusy.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Away)
            {
                borderAway.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Offline)
            {
                borderOffline.BorderThickness = new Thickness(3);
            }

            selectedUserStatus = status;
        }
    }
}
