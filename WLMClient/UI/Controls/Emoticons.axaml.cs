using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using WLMClient.Compat;

using RichTextBox = WLMClient.Compat.RichTextBox;
using BrushConverter = WLMClient.Compat.BrushConverter;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// Interaction logic for Emoticons.axaml
    /// </summary>
    public partial class Emoticons : UserControl
    {
        private RichTextBox richTextBox;

        public Emoticons(RichTextBox richTextBox)
        {
            InitializeComponent();

            this.richTextBox = richTextBox;
            populateList();
        }


        public void populateList()
        {
            int positionX = 0;
            int positionY = 0;
            int count = 0;

            foreach (string emoticon in Resource.Images.Emoticons.INDEX_IN_IMAGE.Keys)
            {
                if (count > 5)
                {
                    positionY += 35;
                    positionX = 0;
                    count = 0;
                }

                CroppedBitmap smiley = Layout.LoadResource.GetEmoticon(emoticon);

                Border border = new Border();
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(2);
                border.Width = 25;
                border.Height = 25;
                border.Margin = new Thickness(positionX, positionY, 0, 0);
                border.VerticalAlignment = VerticalAlignment.Top;
                border.HorizontalAlignment = HorizontalAlignment.Left;
                border.Background = Brushes.Transparent;
                border.Tag = emoticon;

                ToolTip.SetTip(border, emoticon);

                border.PointerEntered += Br_MouseEnter;
                border.PointerExited += Br_MouseLeave;
                border.PointerPressed += Br_MouseDown;

                Image img = new Image();
                img.VerticalAlignment = VerticalAlignment.Center;
                img.HorizontalAlignment = HorizontalAlignment.Center;
                img.Width = 19;
                img.Height = 19;
                img.Source = smiley;

                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                RenderOptions.SetEdgeMode(img, EdgeMode.Aliased);

                positionX += 35;

                border.Child = img;

                gridEmoticons.Children.Add(border);

                count++;
            }
        }

        void Br_MouseDown(object sender, PointerPressedEventArgs e)
        {
            InsertText(((Border)sender).Tag.ToString(), richTextBox);

            this.IsVisible = false;

            e.Handled = true;
        }

        void Br_MouseLeave(object sender, PointerEventArgs e)
        {
            ((Border)sender).BorderBrush = Brushes.Transparent;
        }

        void Br_MouseEnter(object sender, PointerEventArgs e)
        {
            ((Border)sender).BorderBrush = new BrushConverter().ConvertFrom("#afafaf");
        }

        private void InsertText(String text, RichTextBox rtb)
        {
            rtb.InsertText(text);
        }
    }
}
