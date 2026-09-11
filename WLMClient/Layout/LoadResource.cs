using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using System.IO;

using WLMData.Enums;
using WLMClient.UI.Data.Enums;

namespace WLMClient.Layout
{
    class LoadResource
    {
        private static readonly HttpClient httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            return client;
        }

        public static CroppedBitmap GetSmallIconFromStatus(UserStatus status)
        {
            int smallIconsWidth = 0;

            if (status == UserStatus.Offline)
            {
                smallIconsWidth = Resource.Images.Attributes.USER_LIST_STATUS_OFFLINE;
            }

            if (status == UserStatus.Busy)
            {
                smallIconsWidth = Resource.Images.Attributes.USER_LIST_STATUS_BUSY;
            }

            if (status == UserStatus.Away)
            {
                smallIconsWidth = Resource.Images.Attributes.USER_LIST_STATUS_AWAY;
            }

            if (status == UserStatus.Available)
            {
                smallIconsWidth = Resource.Images.Attributes.USER_LIST_STATUS_AVAILABLE;
            }

            return new CroppedBitmap(Images.BITMAP_WINDOW_SMALL_ICONS, new PixelRect(smallIconsWidth, 0,
                Resource.Images.Attributes.USER_LIST_STATUS_WIDTH, Resource.Images.Attributes.USER_LIST_STATUS_HEIGHT));
        }

        public static CroppedBitmap GetAvatarFrameFromStatus(UserStatus status, AvatarSize size)
        {
            int avatarFrameWidth = 0;

            if (status == UserStatus.Offline & size == AvatarSize.Big)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_STATUS_OFFLINE;
            }

            if (status == UserStatus.Busy & size == AvatarSize.Big)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_STATUS_BUSY;
            }

            if (status == UserStatus.Away & size == AvatarSize.Big)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_STATUS_AWAY;
            }

            if (status == UserStatus.Available & size == AvatarSize.Big)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_STATUS_AVAILABLE;
            }

            if (status == UserStatus.Offline & size == AvatarSize.Small)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_SMALL_STATUS_OFFLINE;
            }

            if (status == UserStatus.Busy & size == AvatarSize.Small)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_SMALL_STATUS_BUSY;
            }

            if (status == UserStatus.Away & size == AvatarSize.Small)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_SMALL_STATUS_AWAY;
            }

            if (status == UserStatus.Available & size == AvatarSize.Small)
            {
                avatarFrameWidth = Resource.Images.Attributes.AVATAR_FRAME_SMALL_STATUS_AVAILABLE;
            }

            if (size == AvatarSize.Big)
            {
                return new CroppedBitmap(Images.BITMAP_AVATAR_FRAME, new PixelRect(avatarFrameWidth, 0,
                    Resource.Images.Attributes.AVATAR_FRAME_WIDTH, Resource.Images.Attributes.AVATAR_FRAME_HEIGHT));
            }

            if (size == AvatarSize.Small)
            {
                return new CroppedBitmap(Images.BITMAP_AVATAR_FRAME, new PixelRect(avatarFrameWidth, 0,
                    Resource.Images.Attributes.AVATAR_FRAME_SMALL_WIDTH, Resource.Images.Attributes.AVATAR_FRAME_SMALL_HEIGHT));
            }

            return null;
        }

        public static CroppedBitmap GetDefaultAvatarImage()
        {
            return new CroppedBitmap(Images.BITMAP_AVATAR_FRAME, new PixelRect(
                Resource.Images.Attributes.AVATAR_DEFAULT_IMAGE, 0,
                Resource.Images.Attributes.AVATAR_CHAT_SIZE_WIDTH, Resource.Images.Attributes.AVATAR_CHAT_SIZE_HEIGHT));
        }

        /// <summary>
        /// Redraws an image at a new pixel size. Used for the small avatar on the main window, which
        /// the original scaled down with high quality filtering.
        /// </summary>
        public static IImage Resize(IImage image, int width, int height, BitmapInterpolationMode scalingMode)
        {
            if (image == null)
            {
                return null;
            }

            try
            {
                RenderTargetBitmap target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));

                using (DrawingContext context = target.CreateDrawingContext())
                {
                    context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = scalingMode });

                    image.Draw(context,
                        new Rect(0, 0, image.Size.Width, image.Size.Height),
                        new Rect(0, 0, width, height));
                }

                return target;
            }
            catch
            {
                return image;
            }
        }

        public static CroppedBitmap GetEmoticon(string emoticon)
        {
            return new CroppedBitmap(Images.BITMAP_EMOTICONS,
                new PixelRect(Resource.Images.Emoticons.INDEX_IN_IMAGE[emoticon] * Resource.Images.Attributes.EMOTICON_WIDTH - Resource.Images.Attributes.EMOTICON_WIDTH,
                    0, Resource.Images.Attributes.EMOTICON_WIDTH, Resource.Images.Attributes.EMOTICON_HEIGHT));
        }

        /// <summary>Downloads a user's avatar. Returns null if it cannot be fetched or decoded.</summary>
        public static Bitmap GetAvatar(string avatar)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(avatar))
                {
                    return null;
                }

                byte[] bytes = httpClient.GetByteArrayAsync(avatar.Trim()).GetAwaiter().GetResult();

                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    return new Bitmap(stream);
                }
            }
            catch
            {
            }

            return null;
        }

        public static CroppedBitmap chatWindowButtonSmileys(ButtonState state)
        {
            if (state == ButtonState.None)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(0, 0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            if (state == ButtonState.Hover)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, 0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            if (state == ButtonState.Pressed)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH * 2, 0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                new PixelRect(0, 0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
        }

        public static CroppedBitmap chatWindowButtonNudge(ButtonState state)
        {
            if (state == ButtonState.None)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            if (state == ButtonState.Hover)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            if (state == ButtonState.Pressed)
            {
                return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                    new PixelRect(Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH * 2, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
            }

            return new CroppedBitmap(Images.BITMAP_CHAT_WINDOW_BUTTONS,
                new PixelRect(0, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_WIDTH, Resource.Images.Attributes.CHAT_WINDOW_BUTTONS_HEIGHT));
        }

        /// <summary>The small square bullet drawn in front of each chat line.</summary>
        public static Image getParagraphRectangle()
        {
            Image image = new Image();

            image.Source = Images.BITMAP_CHAT_PARAGRAPH_RECTANGLE;
            image.Width = 3;
            image.Height = 3;
            image.Stretch = Stretch.Fill;

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

            return image;
        }
    }
}
