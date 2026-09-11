using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMClient.Config
{
    class Properties
    {
        public static int SERVER_PORT = 0;
        public static string SERVER_ADDRESS = "";
        public static string AVATAR_IMAGE_UPLOAD_URL = "";
        public static string SERVER_ENCRYPTION_KEY = "CHANGEME";

        /// <summary>Where the "Sign up." link on the sign in page goes. Empty means not configured.</summary>
        public static string REGISTRATION_URL = "";

        /// <summary>The connected server as the user typed it, shown in the main window header.</summary>
        public static string SERVER_DISPLAY = "";

        /// <summary>Port assumed when an address is given without one.</summary>
        public const int DEFAULT_PORT = 1323;

        /// <summary>Renders a host and port the way a user would type them.</summary>
        public static string FormatServer(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return "";
            }

            return port == DEFAULT_PORT || port <= 0 ? host : host + ":" + port;
        }

        /// <summary>What to show as the current server, falling back to the configured address.</summary>
        public static string GetServerDisplay()
        {
            return string.IsNullOrWhiteSpace(SERVER_DISPLAY)
                ? FormatServer(SERVER_ADDRESS, SERVER_PORT)
                : SERVER_DISPLAY;
        }
    }
}
