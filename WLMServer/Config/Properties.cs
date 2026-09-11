using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMServer.Config
{
    class Properties
    {
        public static int SERVER_PORT;
        public static string SERVER_ENCRYPTION_KEY;
        public static string DATABASE_PASSWORD_ENCRYPTION_KEY;
        public static string DATABASE_PASSWORD_ENCRYPTION_IV;
        public static string DATABASE_HOST;
        public static string DATABASE_ID;
        public static string DATABASE_PASSWORD;
        public static bool AVATAR_ENABLE;
        public static string AVATAR_IMAGE_URL;
        public static string AVATAR_IMAGE_UPLOAD_URL;
        public static int BROADCAST_INTERVAL;
        public static int AVATAR_HTTP_PORT;
        public static string AVATAR_STORAGE_PATH;

        /// <summary>Port the built in website listens on. 0 means no website at all.</summary>
        public static int HTTP_PORT;

        /// <summary>Whether the website offers a sign up form.</summary>
        public static bool REGISTRATION_ENABLE;

        /// <summary>
        /// What the client is told to open for "Sign up." Set this when the sign up page is behind
        /// a reverse proxy or hosted elsewhere; otherwise the server works it out from HTTP_PORT.
        /// </summary>
        public static string REGISTRATION_URL;

        /// <summary>Shown as the site's heading, so people know which server they are joining.</summary>
        public static string SERVER_NAME;
    }
}
