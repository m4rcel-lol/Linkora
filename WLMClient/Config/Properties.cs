using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMClient.Config
{
    public class Properties
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

        /// <summary>
        /// Accepts "host", "host:port" and a pasted "scheme://host:port". The port falls back to
        /// whatever the configuration file specifies, then to <see cref="DEFAULT_PORT"/>.
        /// </summary>
        public static bool TryParseServer(string text, out string host, out int port)
        {
            host = null;
            port = SERVER_PORT > 0 ? SERVER_PORT : DEFAULT_PORT;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();

            int scheme = text.IndexOf("://", StringComparison.Ordinal);

            if (scheme >= 0)
            {
                text = text.Substring(scheme + 3);
            }

            text = text.Trim('/');

            // A single colon separates host and port; more than one means a bare IPv6 address,
            // which is passed through untouched.
            int colon = text.IndexOf(':');

            if (colon > 0 && colon == text.LastIndexOf(':'))
            {
                int parsed;

                if (int.TryParse(text.Substring(colon + 1), out parsed) && parsed > 0 && parsed <= 65535)
                {
                    host = text.Substring(0, colon).Trim();
                    port = parsed;

                    return host.Length > 0;
                }

                return false;
            }

            host = text;

            return host.Length > 0;
        }

        /// <summary>
        /// Where the "Sign up." link should go for a given server address. An explicit
        /// registration_url always wins; otherwise the registration page is assumed to be served
        /// from the same host as the server, which is how the bundled one is meant to be deployed.
        /// The messaging port is dropped because the web server is a different service.
        /// </summary>
        public static string GetRegistrationUrl(string serverText)
        {
            if (!string.IsNullOrWhiteSpace(REGISTRATION_URL))
            {
                return REGISTRATION_URL.Trim();
            }

            string host;
            int port;

            if (!TryParseServer(serverText, out host, out port))
            {
                return "";
            }

            // A bare IPv6 address has to be bracketed to be a valid URL host.
            if (host.Contains(":"))
            {
                host = "[" + host + "]";
            }

            return "http://" + host + "/";
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
