using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMClient.Resource.Images
{
    class Identifiers
    {
        // Avalonia addresses embedded resources with avares://, replacing WPF's pack:// URIs.
        public static string CHAT_WINDOW_BACKGROUND_SKINNY = "avares://WLMClient/Content/Interface/default-messenger-skinny.jpg";
        public static string CHAT_WINDOW_BACKGROUND_WIDE = "avares://WLMClient/Content/Interface/default-messenger-wide.jpg";
        public static string MAIN_WINDOW_SMALL_ICONS = "avares://WLMClient/Content/Interface/mainWindowSmallIcons.png";
        public static string CHAT_WINDOW_BUTTONS = "avares://WLMClient/Content/Interface/chatWindowButtons.png";
        public static string EMOTICONS = "avares://WLMClient/Content/Other/emoticons.png";
        public static string AVATAR_FRAMES_PATH = "avares://WLMClient/Content/Interface/chatFrame.png";
        public static string CHAT_PARAGRAPH_RECTANGLE = "avares://WLMClient/Content/Interface/paragraphRectangle.png";

        public static string APP_ICON_STATUS_AVAILABLE = "avares://WLMClient/Content/Icons/161.png";
        public static string APP_ICON_STATUS_AWAY = "avares://WLMClient/Content/Icons/162.png";
        public static string APP_ICON_STATUS_BUSY = "avares://WLMClient/Content/Icons/163.png";
        public static string APP_ICON_STATUS_OFFLINE = "avares://WLMClient/Content/Icons/164.png";
    }
}
