using System;

using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WLMClient
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .AfterSetup(builder => HookInputTracking());
        }

        /// <summary>
        /// Feeds every key and pointer event the application sees into the idle tracker, which is
        /// the fallback used for the automatic away status where no system idle API is available.
        /// </summary>
        private static void HookInputTracking()
        {
            InputElement.KeyDownEvent.AddClassHandler<InputElement>(
                (sender, e) => Compat.Platform.NoteUserInput(), RoutingStrategies.Tunnel);

            InputElement.PointerPressedEvent.AddClassHandler<InputElement>(
                (sender, e) => Compat.Platform.NoteUserInput(), RoutingStrategies.Tunnel);

            InputElement.PointerMovedEvent.AddClassHandler<InputElement>(
                (sender, e) => Compat.Platform.NoteUserInput(), RoutingStrategies.Tunnel);
        }
    }
}
