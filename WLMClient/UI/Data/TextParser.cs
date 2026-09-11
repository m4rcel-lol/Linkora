using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using WLMClient.Compat;
using WLMClient.Compat.Documents;
using WLMClient.Layout;
using WLMClient.Resource.Images;

using AvaloniaInlines = Avalonia.Controls.Documents;

namespace WLMClient.UI.Data
{
    /// <summary>
    /// Turns emoticon shortcuts into images and URLs into clickable links, both in
    /// <see cref="RichTextBox"/> documents and in plain Avalonia text blocks.
    /// </summary>
    class TextParser
    {
        /// <summary>
        /// Remembers the text a TextBlock was last built from. Once parsed the block renders from
        /// its inlines rather than its Text property, so the original string has to be kept.
        /// </summary>
        private static readonly ConditionalWeakTable<TextBlock, string> parsedSources =
            new ConditionalWeakTable<TextBlock, string>();

        private static readonly Regex UrlRegex = new Regex(
            @"((https?|ftp|file)\://|www.)[A-Za-z0-9\.\-]+(/[A-Za-z0-9\?\&\=;\+!'\(\)\*\-\._~%]*)*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void ParseText(object element, bool findLinks)
        {
            RichTextBox textBox = element as RichTextBox;

            if (textBox != null)
            {
                foreach (Paragraph paragraph in textBox.Document.Paragraphs)
                {
                    ProcessInlines(textBox, paragraph.Inlines, findLinks);
                }

                return;
            }

            TextBlock textBlock = element as TextBlock;

            if (textBlock != null)
            {
                ParseTextBlock(textBlock, findLinks);
            }

            // Anything else (a plain TextBox, for instance) is left alone, matching the original.
        }

        #region TextBlock parsing

        /// <summary>Rebuilds a text block's inlines from its text, expanding emoticons and links.</summary>
        private static void ParseTextBlock(TextBlock textBlock, bool findLinks)
        {
            string source = textBlock.Text;

            if (string.IsNullOrEmpty(source))
            {
                // Already parsed: recover the string it was built from.
                if (!parsedSources.TryGetValue(textBlock, out source) || string.IsNullOrEmpty(source))
                {
                    return;
                }
            }

            parsedSources.Remove(textBlock);
            parsedSources.Add(textBlock, source);

            List<Inline> parsed = BuildInlines(source, findLinks);

            DetachInlineChildren(textBlock);

            textBlock.Text = null;

            foreach (Inline inline in parsed)
            {
                Run run = inline as Run;
                if (run != null)
                {
                    textBlock.Inlines.Add(new AvaloniaInlines.Run(run.Text));
                    continue;
                }

                InlineUIContainer container = inline as InlineUIContainer;
                if (container != null && container.Child != null)
                {
                    textBlock.Inlines.Add(new AvaloniaInlines.InlineUIContainer(container.Child)
                    {
                        BaselineAlignment = container.BaselineAlignment
                    });
                }
            }
        }

        private static void DetachInlineChildren(TextBlock textBlock)
        {
            if (textBlock.Inlines == null)
            {
                return;
            }

            foreach (AvaloniaInlines.Inline inline in textBlock.Inlines)
            {
                AvaloniaInlines.InlineUIContainer container = inline as AvaloniaInlines.InlineUIContainer;
                if (container != null)
                {
                    container.Child = null;
                }
            }

            textBlock.Inlines.Clear();
        }

        #endregion

        #region Document parsing

        /// <summary>Expands every emoticon shortcut and, optionally, every URL in a paragraph.</summary>
        public static void ProcessInlines(RichTextBox textBox, InlineCollection inlines, bool findLinks)
        {
            for (int inlineIndex = 0; inlineIndex < inlines.Count; inlineIndex++)
            {
                Run run = inlines[inlineIndex] as Run;

                if (run == null || string.IsNullOrEmpty(run.Text))
                {
                    continue;
                }

                List<Inline> replacement = BuildInlines(run.Text, findLinks);

                // Nothing matched: the run already is the whole result.
                if (replacement.Count == 1 && replacement[0] is Run)
                {
                    continue;
                }

                inlines.ReplaceRange(inlineIndex, 1, replacement);

                inlineIndex += replacement.Count - 1;
            }
        }

        /// <summary>
        /// Splits a string into runs, emoticon images and link blocks. This is the single place
        /// that knows how the original rendered inline content.
        /// </summary>
        private static List<Inline> BuildInlines(string text, bool findLinks)
        {
            List<Inline> result = new List<Inline>();

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            List<Match> links = findLinks
                ? UrlRegex.Matches(text).Cast<Match>().ToList()
                : new List<Match>();

            StringBuilder pending = new StringBuilder();
            int index = 0;

            while (index < text.Length)
            {
                Match link = links.FirstOrDefault(m => m.Index == index);

                if (link != null && link.Length > 0)
                {
                    FlushPending(pending, result);
                    result.Add(CreateLinkInline(link.Value));

                    index += link.Length;

                    continue;
                }

                string emoticon = MatchEmoticonAt(text, index);

                if (emoticon != null)
                {
                    FlushPending(pending, result);
                    result.Add(CreateEmoticonInline(emoticon));

                    index += emoticon.Length;

                    continue;
                }

                pending.Append(text[index]);
                index++;
            }

            FlushPending(pending, result);

            return result;
        }

        private static void FlushPending(StringBuilder pending, List<Inline> result)
        {
            if (pending.Length > 0)
            {
                result.Add(new Run(pending.ToString()));
                pending.Clear();
            }
        }

        /// <summary>Returns the longest emoticon shortcut starting at <paramref name="index"/>.</summary>
        private static string MatchEmoticonAt(string text, int index)
        {
            string best = null;

            foreach (string emoticon in Emoticons.INDEX_IN_IMAGE.Keys)
            {
                if (emoticon.Length == 0 || index + emoticon.Length > text.Length)
                {
                    continue;
                }

                if (string.Compare(text, index, emoticon, 0, emoticon.Length,
                        StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }

                if (best == null || emoticon.Length > best.Length)
                {
                    best = emoticon;
                }
            }

            return best;
        }

        private static Inline CreateEmoticonInline(string emoticon)
        {
            Image image = new Image();

            image.Source = LoadResource.GetEmoticon(emoticon.ToLower());
            image.Width = 19;
            image.Height = 19;
            image.Stretch = Stretch.Fill;

            // The shortcut is kept so the text can be recovered when the message is sent.
            image.Tag = emoticon.ToLower();

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

            return new InlineUIContainer(image)
            {
                BaselineAlignment = BaselineAlignment.TextBottom
            };
        }

        private static Inline CreateLinkInline(string url)
        {
            TextBlock block = new TextBlock();

            block.Text = url;
            block.Cursor = new Cursor(StandardCursorType.Hand);

            // A text block with no background is not hit testable, so it would never be clickable.
            block.Background = Brushes.Transparent;
            block.TextDecorations = TextDecorations.Underline;
            block.Foreground = Brushes.Blue;
            block.Tag = url;

            block.PointerPressed += (sender, e) => OpenUrl(((TextBlock)sender).Text);

            return new InlineUIContainer(block)
            {
                BaselineAlignment = BaselineAlignment.TextBottom
            };
        }

        #endregion

        public static List<string> GetLinks(string message)
        {
            List<string> list = new List<string>();

            foreach (Match match in UrlRegex.Matches(message))
            {
                list.Add(match.Value);
            }

            return list;
        }

        /// <summary>The document's text, with emoticon images turned back into their shortcuts.</summary>
        public static string GetPlainText(FlowDocument doc)
        {
            StringBuilder result = new StringBuilder();

            foreach (Paragraph paragraph in doc.Paragraphs)
            {
                RichTextBox.AppendParagraphText(paragraph, result);
                result.Append(Environment.NewLine);
            }

            return result.ToString();
        }

        /// <summary>Opens a link in the user's browser on any of the three desktop platforms.</summary>
        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            string target = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + url : url;

            try
            {
                ProcessStartInfo startInfo;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    startInfo = new ProcessStartInfo("open", "\"" + target + "\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    startInfo = new ProcessStartInfo("xdg-open", "\"" + target + "\"");
                }
                else
                {
                    startInfo = new ProcessStartInfo(target) { UseShellExecute = true };
                }

                startInfo.CreateNoWindow = true;

                Process.Start(startInfo);
            }
            catch
            {
                // No browser available; nothing sensible to show the user here.
            }
        }
    }
}
