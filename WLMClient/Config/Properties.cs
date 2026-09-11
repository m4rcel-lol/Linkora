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
    }
}
