using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using WLMClient.UI.Windows;

namespace WLMClient
{
    /// <summary>
    /// Application entry point. Replaces WPF's StartupUri by creating the main window once the
    /// framework has finished initialising.
    /// </summary>
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IClassicDesktopStyleApplicationLifetime desktop =
                ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

            if (desktop != null)
            {
                // Chat windows stay open independently, so only an explicit exit closes the app.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow = new MainWindow();

                Compat.ScreenshotHelper.InstallIfRequested();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
