using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WLMShared;

using WLMServer.Network;

namespace WLMServer
{
    class Program
    {
        private static Server server;

        static void Main(string[] args)
        {
            SetConsoleTitle("Linkora Server");

            Console.WriteLine("Type /help to see commands.");

            try
            {
                ImportMessengerConfig();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Failed to read configuration: " + exception.Message);
                Console.WriteLine("Expected settings file: " + ConfigurationManager.SettingsFilePath);

                return;
            }

            try
            {
                server = new Server();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Failed to start the server: " + exception.Message);

                return;
            }

            while (true)
            {
                string input = Console.ReadLine();

                if (input == null)
                {
                    // No console attached (for example running under systemd or launchd). The
                    // server threads keep running, so just sleep instead of spinning.
                    Thread.Sleep(Timeout.Infinite);

                    return;
                }

                if (input.ToLower() == "/help")
                {
                    SafeClear();
                    Console.WriteLine("----COMMANDS----");
                    Console.WriteLine("1. /help           - Shows commands");
                    Console.WriteLine("2. /online         - Get online count");
                    Console.WriteLine("3. /listonline     - Shows list of online users");
                    Console.WriteLine("4. /create         - Register new account");
                    Console.WriteLine("---------------");
                }

                if (input.ToLower() == "/online")
                {
                    WriteToConsole("There are " + server.GetUserCount() + " users currently online.");
                }

                if (input.ToLower() == "/listonline")
                {
                    server.ConsoleEchoOnlineUsers();
                }

                if (input.ToLower() == "/create")
                {
                    Console.WriteLine("Type a username");
                    string inputUsername = (Console.ReadLine() ?? "").Trim();
                    Console.WriteLine("Type a password");
                    string inputPassword = (Console.ReadLine() ?? "").Trim();
                    WriteToConsole(server.ConsoleRegisterNewUser(inputUsername, inputPassword));
                }
            }
        }

        public static void WriteToConsole(string input)
        {
            Console.WriteLine(DateTime.Now.ToString() + ": " + input);
        }

        /// <summary>Setting the console title is not supported on every terminal, so never fail on it.</summary>
        private static void SetConsoleTitle(string title)
        {
            try
            {
                Console.Title = title;
            }
            catch
            {
            }
        }

        private static void SafeClear()
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // Redirected output has no screen to clear.
            }
        }

        public static void ImportMessengerConfig()
        {
            Config.Properties.DATABASE_HOST = RequireSetting("database_host");
            Config.Properties.DATABASE_ID = RequireSetting("database_id");
            Config.Properties.DATABASE_PASSWORD = ConfigurationManager.AppSettings["database_password"] ?? "";
            Config.Properties.SERVER_PORT = Convert.ToInt32(RequireSetting("server_port"));
            Config.Properties.SERVER_ENCRYPTION_KEY = RequireSetting("server_encryption_key");
            Config.Properties.DATABASE_PASSWORD_ENCRYPTION_KEY = RequireSetting("database_password_encryption_key");
            Config.Properties.DATABASE_PASSWORD_ENCRYPTION_IV = RequireSetting("database_password_encryption_iv");
            Config.Properties.BROADCAST_INTERVAL = Convert.ToInt32(RequireSetting("broadcast_interval"));
            Config.Properties.AVATAR_ENABLE = Convert.ToBoolean(RequireSetting("avatars_enabled"));
            Config.Properties.AVATAR_IMAGE_URL = ConfigurationManager.AppSettings["avatars_address"] ?? "";
            Config.Properties.AVATAR_IMAGE_UPLOAD_URL = ConfigurationManager.AppSettings["avatars_address_upload"] ?? "";

            // Optional built-in avatar webserver. 0 disables it, in which case avatars need an
            // external webserver running the bundled upload.php.
            Config.Properties.AVATAR_HTTP_PORT = Convert.ToInt32(
                ConfigurationManager.AppSettings["avatars_http_port"] ?? "0");
            Config.Properties.AVATAR_STORAGE_PATH = ConfigurationManager.AppSettings["avatars_storage_path"] ?? "uploads";
        }

        private static string RequireSetting(string key)
        {
            string value = ConfigurationManager.AppSettings[key];

            if (value == null)
            {
                throw new Exception("Missing '" + key + "' in Messenger.config.");
            }

            return value;
        }
    }
}
