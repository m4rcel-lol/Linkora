using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

using WLMClient.Resource.Images;

namespace WLMClient.Layout
{
    class Images
    {
        // Sprite sheets the rest of the UI crops individual icons out of.
        public static Bitmap BITMAP_AVATAR_FRAME;
        public static Bitmap BITMAP_CHAT_WINDOW_BUTTONS;
        public static Bitmap BITMAP_EMOTICONS;
        public static Bitmap BITMAP_WINDOW_SMALL_ICONS;
        public static Bitmap BITMAP_CHAT_PARAGRAPH_RECTANGLE;

        /// <summary>Loads an image embedded in the application.</summary>
        public static Bitmap LoadBitmap(string url)
        {
            return new Bitmap(AssetLoader.Open(new Uri(url)));
        }

        public static void Load()
        {
            BITMAP_AVATAR_FRAME = LoadBitmap(Identifiers.AVATAR_FRAMES_PATH);
            BITMAP_CHAT_WINDOW_BUTTONS = LoadBitmap(Identifiers.CHAT_WINDOW_BUTTONS);
            BITMAP_EMOTICONS = LoadBitmap(Identifiers.EMOTICONS);
            BITMAP_WINDOW_SMALL_ICONS = LoadBitmap(Identifiers.MAIN_WINDOW_SMALL_ICONS);
            BITMAP_CHAT_PARAGRAPH_RECTANGLE = LoadBitmap(Identifiers.CHAT_PARAGRAPH_RECTANGLE);

            Emoticons.INDEX_IN_IMAGE[":)"] = 1;
            Emoticons.INDEX_IN_IMAGE[":d"] = 2;
            Emoticons.INDEX_IN_IMAGE[";)"] = 3;
            Emoticons.INDEX_IN_IMAGE[":o"] = 4;
            Emoticons.INDEX_IN_IMAGE[":p"] = 5;
            Emoticons.INDEX_IN_IMAGE["(h)"] = 6;
            Emoticons.INDEX_IN_IMAGE[":@"] = 7;
            Emoticons.INDEX_IN_IMAGE[":§"] = 8;
            Emoticons.INDEX_IN_IMAGE[":s"] = 9;
            Emoticons.INDEX_IN_IMAGE[":("] = 10;
            Emoticons.INDEX_IN_IMAGE[":'("] = 11;
            Emoticons.INDEX_IN_IMAGE[":|"] = 12;
            Emoticons.INDEX_IN_IMAGE["(6)"] = 13;
            Emoticons.INDEX_IN_IMAGE["(a)"] = 14;
            Emoticons.INDEX_IN_IMAGE["(l)"] = 15;
            Emoticons.INDEX_IN_IMAGE["(u)"] = 16;

            Emoticons.INDEX_IN_IMAGE["(s)"] = 20;
            Emoticons.INDEX_IN_IMAGE["(*)"] = 21;
            Emoticons.INDEX_IN_IMAGE["(8)"] = 23;
            Emoticons.INDEX_IN_IMAGE["(f)"] = 25;
            Emoticons.INDEX_IN_IMAGE["(w)"] = 26;
            Emoticons.INDEX_IN_IMAGE["(o)"] = 27;
            Emoticons.INDEX_IN_IMAGE["(k)"] = 28;
            Emoticons.INDEX_IN_IMAGE["(g)"] = 29;
            Emoticons.INDEX_IN_IMAGE["(^)"] = 30;
            Emoticons.INDEX_IN_IMAGE["(i)"] = 32;
            Emoticons.INDEX_IN_IMAGE["(c)"] = 33;
            Emoticons.INDEX_IN_IMAGE["(b)"] = 37;
            Emoticons.INDEX_IN_IMAGE["(d)"] = 38;

            Emoticons.INDEX_IN_IMAGE["(y)"] = 41;
            Emoticons.INDEX_IN_IMAGE["(n)"] = 42;
            Emoticons.INDEX_IN_IMAGE[":["] = 43;
            Emoticons.INDEX_IN_IMAGE[":-#"] = 48;
            Emoticons.INDEX_IN_IMAGE["8o|"] = 49;
            Emoticons.INDEX_IN_IMAGE["8-|"] = 50;
            Emoticons.INDEX_IN_IMAGE["^o)"] = 51;
            Emoticons.INDEX_IN_IMAGE["+o("] = 53;
            Emoticons.INDEX_IN_IMAGE["(sn)"] = 54;
            Emoticons.INDEX_IN_IMAGE["(pi)"] = 58;
            Emoticons.INDEX_IN_IMAGE["(bah)"] = 71;
            Emoticons.INDEX_IN_IMAGE["<:o)"] = 75;
            Emoticons.INDEX_IN_IMAGE["(ci)"] = 77;
        }
    }
}
