using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using WLMClient.Compat.Documents;

using AvaloniaInlines = Avalonia.Controls.Documents;

namespace WLMClient.Compat
{
    /// <summary>
    /// A rich text control with the same shape as the WPF one the application was written against:
    /// a <see cref="Documents.FlowDocument"/> of paragraphs holding text and embedded controls,
    /// with a caret, selection and clipboard support.
    /// </summary>
    public class RichTextBox : ContentControl
    {
        private readonly ScrollViewer scrollViewer;
        private readonly StackPanel paragraphHost;
        private readonly DispatcherTimer caretTimer;
        private readonly List<ParagraphVisual> visuals = new List<ParagraphVisual>();

        private FlowDocument document;
        private int caretIndex;
        private int selectionAnchor;
        private bool caretOn;
        private bool isSelecting;
        private bool renderQueued;
        private object toolTipValue;

        /// <summary>Per paragraph visual tree: the border, the text and the caret/selection layers.</summary>
        private sealed class ParagraphVisual
        {
            public Paragraph Paragraph;
            public Border Border;
            public TextBlock Text;
            public Canvas SelectionLayer;
            public Rectangle Caret;
            public int StartIndex;
        }

        public RichTextBox()
        {
            Focusable = true;
            Padding = new Thickness(2);
            Background = Brushes.White;
            BorderThickness = new Thickness(0);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;

            paragraphHost = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            scrollViewer = new ScrollViewer
            {
                Content = paragraphHost,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };

            base.Content = scrollViewer;

            Document = new FlowDocument();

            caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
            caretTimer.Tick += (s, e) =>
            {
                caretOn = !caretOn;
                UpdateCaretVisual();
            };
        }

        /// <summary>
        /// Avalonia resolves control themes by exact runtime type, so a derived control would get
        /// no template. Borrow the ContentControl one, which draws the background and border.
        /// </summary>
        protected override Type StyleKeyOverride
        {
            get { return typeof(ContentControl); }
        }

        #region Public surface used by the application

        public FlowDocument Document
        {
            get { return document; }
            set
            {
                if (document != null)
                {
                    document.Changed -= OnDocumentChanged;
                }

                document = value ?? new FlowDocument();
                document.Changed += OnDocumentChanged;

                caretIndex = 0;
                selectionAnchor = 0;

                QueueRender();
            }
        }

        public bool IsReadOnly { get; set; }

        /// <summary>Present for source compatibility with WPF; hit testing is always enabled here.</summary>
        public bool IsDocumentEnabled { get; set; }

        public bool AcceptsReturn { get; set; }

        /// <summary>Mirrors the WPF ToolTip property while forwarding to Avalonia's attached tip.</summary>
        public object ToolTip
        {
            get { return toolTipValue; }
            set
            {
                toolTipValue = value;
                Avalonia.Controls.ToolTip.SetTip(this, value);
            }
        }

        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return scrollViewer.HorizontalScrollBarVisibility; }
            set
            {
                // Hidden would let content run past the edge; the WPF original relied on wrapping,
                // so treat it as disabled to keep the text inside the control.
                scrollViewer.HorizontalScrollBarVisibility =
                    value == ScrollBarVisibility.Hidden ? ScrollBarVisibility.Disabled : value;
            }
        }

        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return scrollViewer.VerticalScrollBarVisibility; }
            set { scrollViewer.VerticalScrollBarVisibility = value; }
        }

        /// <summary>Raised after any change to the document's text content.</summary>
        public event EventHandler TextChanged;

        /// <summary>Raised before the control handles a key, mirroring WPF's tunnelling events.</summary>
        public event EventHandler<KeyEventArgs> PreviewKeyDown;

        public event EventHandler<KeyEventArgs> PreviewKeyUp;

        public void ScrollToEnd()
        {
            // Let layout settle first, otherwise the extent is still the pre-insert one.
            Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd(), DispatcherPriority.Loaded);
        }

        /// <summary>Inserts text at the caret, splitting paragraphs on newlines.</summary>
        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            DeleteSelection();
            EnsureParagraph();

            foreach (char character in text)
            {
                if (character == '\r')
                {
                    continue;
                }

                if (character == '\n')
                {
                    SplitParagraphAtCaret();
                }
                else
                {
                    InsertCharacter(character);
                }
            }

            selectionAnchor = caretIndex;

            RaiseTextChanged();
            QueueRender();
        }

        /// <summary>Inserts an already built inline (an emoticon image) at the caret.</summary>
        public void InsertInline(Inline inline)
        {
            DeleteSelection();
            EnsureParagraph();

            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            Paragraph paragraph = GetParagraph(paragraphIndex);
            int inlineIndex;
            int inlineOffset;
            LocateInline(paragraph, offset, out inlineIndex, out inlineOffset);

            SplitRunAt(paragraph, ref inlineIndex, inlineOffset);
            paragraph.Inlines.Insert(inlineIndex, inline);

            caretIndex++;
            selectionAnchor = caretIndex;

            RaiseTextChanged();
            QueueRender();
        }

        /// <summary>The document's text, with each embedded emoticon rendered back to its shortcut.</summary>
        public string GetPlainText()
        {
            StringBuilder builder = new StringBuilder();

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                AppendParagraphText(paragraph, builder);
                builder.Append(Environment.NewLine);
            }

            return builder.ToString();
        }

        internal static void AppendParagraphText(Paragraph paragraph, StringBuilder builder)
        {
            foreach (Inline inline in paragraph.Inlines)
            {
                Run run = inline as Run;
                if (run != null)
                {
                    builder.Append(run.Text);
                    continue;
                }

                InlineUIContainer container = inline as InlineUIContainer;
                if (container != null && container.Child != null && container.Child.Tag != null)
                {
                    builder.Append(container.Child.Tag.ToString());
                }
                else if (container != null)
                {
                    TextBlock block = container.Child as TextBlock;
                    if (block != null)
                    {
                        builder.Append(block.Text);
                    }
                }
            }
        }

        #endregion

        #region Rendering

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            QueueRender();
        }

        /// <summary>Coalesces the many small document edits made in a row into one re-render.</summary>
        private void QueueRender()
        {
            if (renderQueued)
            {
                return;
            }

            renderQueued = true;

            Dispatcher.UIThread.Post(() =>
            {
                renderQueued = false;
                Render();
            }, DispatcherPriority.Render);
        }

        private void Render()
        {
            // Detach embedded controls before rebuilding so they can be re-parented.
            foreach (ParagraphVisual visual in visuals)
            {
                DetachInlineChildren(visual.Text);
            }

            paragraphHost.Children.Clear();
            visuals.Clear();

            int runningIndex = 0;

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                ParagraphVisual visual = BuildParagraphVisual(paragraph, runningIndex);

                visuals.Add(visual);
                paragraphHost.Children.Add(visual.Border);

                runningIndex += paragraph.GetLogicalLength() + 1;
            }

            Dispatcher.UIThread.Post(UpdateCaretAndSelectionVisuals, DispatcherPriority.Loaded);
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

        private ParagraphVisual BuildParagraphVisual(Paragraph paragraph, int startIndex)
        {
            TextBlock textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = paragraph.TextAlignment,
                Foreground = paragraph.Foreground ?? Foreground,
                FontWeight = paragraph.FontWeight,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (!double.IsNaN(paragraph.FontSize) && paragraph.FontSize > 0)
            {
                textBlock.FontSize = paragraph.FontSize;
            }
            else
            {
                textBlock.FontSize = FontSize;
            }

            if (paragraph.FontFamily != null)
            {
                textBlock.FontFamily = paragraph.FontFamily;
            }

            // WPF ignores a LineHeight smaller than the natural line box unless the paragraph opts
            // into BlockLineHeight, and the original sets values like 0.1 purely to tighten runs.
            if (!double.IsNaN(paragraph.LineHeight) && paragraph.LineHeight >= 1)
            {
                textBlock.LineHeight = paragraph.LineHeight;
            }

            foreach (Inline inline in paragraph.Inlines)
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
                    DetachFromParent(container.Child);

                    textBlock.Inlines.Add(new AvaloniaInlines.InlineUIContainer(container.Child)
                    {
                        BaselineAlignment = container.BaselineAlignment
                    });
                }
            }

            Canvas selectionLayer = new Canvas { IsHitTestVisible = false, ZIndex = 0 };
            Canvas caretLayer = new Canvas { IsHitTestVisible = false, ZIndex = 2 };

            Rectangle caret = new Rectangle
            {
                Width = 1,
                Height = 12,
                Fill = Foreground ?? Brushes.Black,
                IsVisible = false
            };

            caretLayer.Children.Add(caret);
            textBlock.ZIndex = 1;

            Grid layers = new Grid();
            layers.Children.Add(selectionLayer);
            layers.Children.Add(textBlock);
            layers.Children.Add(caretLayer);

            Border border = new Border
            {
                Margin = paragraph.Margin,
                Padding = paragraph.Padding,
                BorderThickness = paragraph.BorderThickness,
                BorderBrush = paragraph.BorderBrush,
                Child = layers
            };

            return new ParagraphVisual
            {
                Paragraph = paragraph,
                Border = border,
                Text = textBlock,
                SelectionLayer = selectionLayer,
                Caret = caret,
                StartIndex = startIndex
            };
        }

        private static void DetachFromParent(Control control)
        {
            Panel panel = control.Parent as Panel;
            if (panel != null)
            {
                panel.Children.Remove(control);
                return;
            }

            Border border = control.Parent as Border;
            if (border != null && ReferenceEquals(border.Child, control))
            {
                border.Child = null;
            }
        }

        #endregion

        #region Caret and selection

        private void UpdateCaretAndSelectionVisuals()
        {
            UpdateSelectionVisual();
            UpdateCaretVisual();
        }

        private bool CaretVisible
        {
            get { return !IsReadOnly && IsFocused; }
        }

        private void UpdateCaretVisual()
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            for (int i = 0; i < visuals.Count; i++)
            {
                ParagraphVisual visual = visuals[i];

                if (i != paragraphIndex || !CaretVisible || !caretOn)
                {
                    visual.Caret.IsVisible = false;
                    continue;
                }

                try
                {
                    Rect rect = visual.Text.TextLayout.HitTestTextPosition(Math.Max(0, offset));

                    Canvas.SetLeft(visual.Caret, rect.X);
                    Canvas.SetTop(visual.Caret, rect.Y);

                    visual.Caret.Height = rect.Height > 0 ? rect.Height : visual.Text.FontSize;
                    visual.Caret.Fill = visual.Text.Foreground ?? Brushes.Black;
                    visual.Caret.IsVisible = true;
                }
                catch
                {
                    visual.Caret.IsVisible = false;
                }
            }
        }

        private void UpdateSelectionVisual()
        {
            int start = Math.Min(selectionAnchor, caretIndex);
            int end = Math.Max(selectionAnchor, caretIndex);

            IBrush highlight = new SolidColorBrush(Color.FromArgb(120, 51, 153, 255));

            foreach (ParagraphVisual visual in visuals)
            {
                visual.SelectionLayer.Children.Clear();

                if (start == end)
                {
                    continue;
                }

                int paragraphLength = visual.Paragraph.GetLogicalLength();
                int localStart = Math.Max(0, start - visual.StartIndex);
                int localEnd = Math.Min(paragraphLength, end - visual.StartIndex);

                if (localEnd <= localStart)
                {
                    continue;
                }

                try
                {
                    IEnumerable<Rect> rects =
                        visual.Text.TextLayout.HitTestTextRange(localStart, localEnd - localStart);

                    foreach (Rect rect in rects)
                    {
                        Rectangle highlightRect = new Rectangle
                        {
                            Width = Math.Max(1, rect.Width),
                            Height = rect.Height,
                            Fill = highlight
                        };

                        Canvas.SetLeft(highlightRect, rect.X);
                        Canvas.SetTop(highlightRect, rect.Y);

                        visual.SelectionLayer.Children.Add(highlightRect);
                    }
                }
                catch
                {
                    // Layout not ready yet; the next render pass will draw the selection.
                }
            }
        }

        private void RestartCaretBlink()
        {
            caretOn = true;
            caretTimer.Stop();

            if (CaretVisible)
            {
                caretTimer.Start();
            }

            UpdateCaretVisual();
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            RestartCaretBlink();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            caretTimer.Stop();
            caretOn = false;

            UpdateCaretVisual();
        }

        #endregion

        #region Index mapping

        private Paragraph GetParagraph(int index)
        {
            List<Paragraph> paragraphs = document.Paragraphs.ToList();

            if (index < 0 || index >= paragraphs.Count)
            {
                return null;
            }

            return paragraphs[index];
        }

        private int TotalLength()
        {
            int total = 0;
            int count = 0;

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                total += paragraph.GetLogicalLength();
                count++;
            }

            return total + Math.Max(0, count - 1);
        }

        /// <summary>Maps a document wide caret index onto a paragraph and an offset inside it.</summary>
        private void ResolveIndex(int index, out int paragraphIndex, out int offset)
        {
            paragraphIndex = 0;
            offset = 0;

            int running = 0;
            int i = 0;

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                int length = paragraph.GetLogicalLength();

                if (index <= running + length)
                {
                    paragraphIndex = i;
                    offset = Math.Max(0, index - running);

                    return;
                }

                running += length + 1;
                i++;
            }

            paragraphIndex = Math.Max(0, i - 1);

            Paragraph last = GetParagraph(paragraphIndex);
            offset = last == null ? 0 : last.GetLogicalLength();
        }

        private int IndexOfParagraphStart(int paragraphIndex)
        {
            int running = 0;
            int i = 0;

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                if (i == paragraphIndex)
                {
                    return running;
                }

                running += paragraph.GetLogicalLength() + 1;
                i++;
            }

            return running;
        }

        /// <summary>Finds which inline a paragraph offset falls in, and how far into it.</summary>
        private static void LocateInline(Paragraph paragraph, int offset, out int inlineIndex, out int inlineOffset)
        {
            inlineIndex = 0;
            inlineOffset = 0;

            int running = 0;

            for (int i = 0; i < paragraph.Inlines.Count; i++)
            {
                Run run = paragraph.Inlines[i] as Run;
                int length = run != null ? run.Text.Length : 1;

                if (offset <= running + length)
                {
                    inlineIndex = i;
                    inlineOffset = offset - running;

                    // Prefer staying at the end of a text run over jumping into an image.
                    if (run == null && inlineOffset == 0 && offset == running)
                    {
                        inlineIndex = i;
                        inlineOffset = 0;
                    }

                    return;
                }

                running += length;
            }

            inlineIndex = paragraph.Inlines.Count;
            inlineOffset = 0;
        }

        /// <summary>Splits the run at the given offset so an inline can be inserted between them.</summary>
        private static void SplitRunAt(Paragraph paragraph, ref int inlineIndex, int inlineOffset)
        {
            if (inlineIndex >= paragraph.Inlines.Count)
            {
                inlineIndex = paragraph.Inlines.Count;
                return;
            }

            Run run = paragraph.Inlines[inlineIndex] as Run;

            if (run == null)
            {
                if (inlineOffset > 0)
                {
                    inlineIndex++;
                }

                return;
            }

            if (inlineOffset <= 0)
            {
                return;
            }

            if (inlineOffset >= run.Text.Length)
            {
                inlineIndex++;
                return;
            }

            string tail = run.Text.Substring(inlineOffset);
            run.Text = run.Text.Substring(0, inlineOffset);

            paragraph.Inlines.Insert(inlineIndex + 1, new Run(tail));
            inlineIndex++;
        }

        #endregion

        #region Editing primitives

        private void EnsureParagraph()
        {
            if (!document.Paragraphs.Any())
            {
                document.Blocks.Add(new Paragraph());
            }
        }

        private void InsertCharacter(char character)
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            Paragraph paragraph = GetParagraph(paragraphIndex);
            if (paragraph == null)
            {
                return;
            }

            int inlineIndex;
            int inlineOffset;
            LocateInline(paragraph, offset, out inlineIndex, out inlineOffset);

            Run target = inlineIndex < paragraph.Inlines.Count ? paragraph.Inlines[inlineIndex] as Run : null;

            if (target != null)
            {
                target.Text = target.Text.Insert(Math.Min(inlineOffset, target.Text.Length), character.ToString());
            }
            else
            {
                // Caret sits on an image boundary or past the end: start a new run.
                int insertAt = inlineIndex;

                if (inlineIndex < paragraph.Inlines.Count && inlineOffset > 0)
                {
                    insertAt = inlineIndex + 1;
                }

                paragraph.Inlines.Insert(Math.Min(insertAt, paragraph.Inlines.Count), new Run(character.ToString()));
            }

            caretIndex++;
        }

        private void SplitParagraphAtCaret()
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            Paragraph paragraph = GetParagraph(paragraphIndex);
            if (paragraph == null)
            {
                return;
            }

            int inlineIndex;
            int inlineOffset;
            LocateInline(paragraph, offset, out inlineIndex, out inlineOffset);
            SplitRunAt(paragraph, ref inlineIndex, inlineOffset);

            Paragraph tail = new Paragraph
            {
                Margin = paragraph.Margin,
                Padding = paragraph.Padding,
                Foreground = paragraph.Foreground,
                FontSize = paragraph.FontSize,
                FontWeight = paragraph.FontWeight,
                FontFamily = paragraph.FontFamily,
                TextAlignment = paragraph.TextAlignment,
                LineHeight = paragraph.LineHeight,
                LineStackingStrategy = paragraph.LineStackingStrategy
            };

            List<Inline> moved = new List<Inline>();

            for (int i = inlineIndex; i < paragraph.Inlines.Count; i++)
            {
                moved.Add(paragraph.Inlines[i]);
            }

            paragraph.Inlines.ReplaceRange(inlineIndex, moved.Count, new List<Inline>());

            foreach (Inline inline in moved)
            {
                tail.Inlines.Add(inline);
            }

            int blockIndex = IndexOfBlock(paragraph);
            document.Blocks.Insert(blockIndex + 1, tail);

            caretIndex++;
        }

        private int IndexOfBlock(Block block)
        {
            for (int i = 0; i < document.Blocks.Count; i++)
            {
                if (ReferenceEquals(document.Blocks[i], block))
                {
                    return i;
                }
            }

            return document.Blocks.Count - 1;
        }

        /// <summary>Removes the character or embedded control immediately before the caret.</summary>
        private void DeleteBackward()
        {
            if (caretIndex <= 0)
            {
                return;
            }

            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            if (offset == 0)
            {
                MergeWithPreviousParagraph(paragraphIndex);
                return;
            }

            Paragraph paragraph = GetParagraph(paragraphIndex);
            RemoveRange(paragraph, offset - 1, 1);

            caretIndex--;
        }

        private void DeleteForward()
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            Paragraph paragraph = GetParagraph(paragraphIndex);
            if (paragraph == null)
            {
                return;
            }

            if (offset >= paragraph.GetLogicalLength())
            {
                if (paragraphIndex + 1 < document.Blocks.Count)
                {
                    MergeWithPreviousParagraph(paragraphIndex + 1);
                    caretIndex = IndexOfParagraphStart(paragraphIndex) + offset;
                }

                return;
            }

            RemoveRange(paragraph, offset, 1);
        }

        private void MergeWithPreviousParagraph(int paragraphIndex)
        {
            if (paragraphIndex <= 0)
            {
                return;
            }

            Paragraph previous = GetParagraph(paragraphIndex - 1);
            Paragraph current = GetParagraph(paragraphIndex);

            if (previous == null || current == null)
            {
                return;
            }

            int newCaret = IndexOfParagraphStart(paragraphIndex - 1) + previous.GetLogicalLength();

            List<Inline> moved = current.Inlines.ToList();
            current.Inlines.Clear();

            foreach (Inline inline in moved)
            {
                previous.Inlines.Add(inline);
            }

            document.Blocks.Remove(current);

            caretIndex = newCaret;
        }

        /// <summary>Removes <paramref name="count"/> logical characters starting at an offset.</summary>
        private static void RemoveRange(Paragraph paragraph, int offset, int count)
        {
            if (paragraph == null || count <= 0)
            {
                return;
            }

            int remaining = count;
            int position = 0;
            int i = 0;

            while (i < paragraph.Inlines.Count && remaining > 0)
            {
                Run run = paragraph.Inlines[i] as Run;
                int length = run != null ? run.Text.Length : 1;
                int inlineStart = position;
                int inlineEnd = position + length;

                if (inlineEnd <= offset)
                {
                    position = inlineEnd;
                    i++;

                    continue;
                }

                if (run == null)
                {
                    paragraph.Inlines.RemoveAt(i);
                    remaining--;

                    continue;
                }

                int localStart = Math.Max(0, offset - inlineStart);
                int removable = Math.Min(run.Text.Length - localStart, remaining);

                run.Text = run.Text.Remove(localStart, removable);
                remaining -= removable;

                if (run.Text.Length == 0)
                {
                    paragraph.Inlines.RemoveAt(i);
                }
                else
                {
                    position = inlineStart + run.Text.Length;
                    i++;
                }
            }
        }

        private bool HasSelection
        {
            get { return selectionAnchor != caretIndex; }
        }

        private void DeleteSelection()
        {
            if (!HasSelection)
            {
                return;
            }

            int start = Math.Min(selectionAnchor, caretIndex);
            int end = Math.Max(selectionAnchor, caretIndex);

            // Delete backwards from the end so indices stay valid as paragraphs merge.
            caretIndex = end;

            for (int i = 0; i < end - start; i++)
            {
                DeleteBackward();
            }

            caretIndex = start;
            selectionAnchor = start;
        }

        private string GetSelectedText()
        {
            if (!HasSelection)
            {
                return string.Empty;
            }

            int start = Math.Min(selectionAnchor, caretIndex);
            int end = Math.Max(selectionAnchor, caretIndex);

            StringBuilder all = new StringBuilder();
            bool first = true;

            foreach (Paragraph paragraph in document.Paragraphs)
            {
                if (!first)
                {
                    all.Append('\n');
                }

                AppendParagraphText(paragraph, all);
                first = false;
            }

            string text = all.ToString();

            start = Math.Min(start, text.Length);
            end = Math.Min(end, text.Length);

            return text.Substring(start, Math.Max(0, end - start));
        }

        private void RaiseTextChanged()
        {
            EventHandler handler = TextChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Input

        protected override void OnKeyDown(KeyEventArgs e)
        {
            EventHandler<KeyEventArgs> preview = PreviewKeyDown;
            if (preview != null)
            {
                preview(this, e);
            }

            if (e.Handled)
            {
                base.OnKeyDown(e);
                return;
            }

            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            bool command = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Meta);

            switch (e.Key)
            {
                case Key.Left:
                    MoveCaret(caretIndex - 1, shift);
                    e.Handled = true;
                    break;

                case Key.Right:
                    MoveCaret(caretIndex + 1, shift);
                    e.Handled = true;
                    break;

                case Key.Up:
                    MoveCaretByLine(-1, shift);
                    e.Handled = true;
                    break;

                case Key.Down:
                    MoveCaretByLine(1, shift);
                    e.Handled = true;
                    break;

                case Key.Home:
                    MoveCaretToParagraphEdge(false, shift);
                    e.Handled = true;
                    break;

                case Key.End:
                    MoveCaretToParagraphEdge(true, shift);
                    e.Handled = true;
                    break;

                case Key.Back:
                    if (!IsReadOnly)
                    {
                        if (HasSelection)
                        {
                            DeleteSelection();
                        }
                        else
                        {
                            DeleteBackward();
                            selectionAnchor = caretIndex;
                        }

                        RaiseTextChanged();
                        QueueRender();
                    }

                    e.Handled = true;
                    break;

                case Key.Delete:
                    if (!IsReadOnly)
                    {
                        if (HasSelection)
                        {
                            DeleteSelection();
                        }
                        else
                        {
                            DeleteForward();
                            selectionAnchor = caretIndex;
                        }

                        RaiseTextChanged();
                        QueueRender();
                    }

                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (!IsReadOnly && AcceptsReturn)
                    {
                        InsertText("\n");
                    }

                    e.Handled = true;
                    break;

                case Key.A:
                    if (command)
                    {
                        selectionAnchor = 0;
                        caretIndex = TotalLength();

                        UpdateCaretAndSelectionVisuals();
                        e.Handled = true;
                    }

                    break;

                case Key.C:
                    if (command)
                    {
                        CopyToClipboard();
                        e.Handled = true;
                    }

                    break;

                case Key.X:
                    if (command)
                    {
                        CopyToClipboard();

                        if (!IsReadOnly)
                        {
                            DeleteSelection();
                            RaiseTextChanged();
                            QueueRender();
                        }

                        e.Handled = true;
                    }

                    break;

                case Key.V:
                    if (command && !IsReadOnly)
                    {
                        PasteFromClipboard();
                        e.Handled = true;
                    }

                    break;
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            EventHandler<KeyEventArgs> preview = PreviewKeyUp;
            if (preview != null)
            {
                preview(this, e);
            }

            base.OnKeyUp(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (!IsReadOnly && !string.IsNullOrEmpty(e.Text))
            {
                InsertText(e.Text);
                RestartCaretBlink();

                e.Handled = true;
            }

            base.OnTextInput(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Focus();

            int index;
            if (TryHitTest(e.GetPosition(this), out index))
            {
                caretIndex = index;
                selectionAnchor = index;
                isSelecting = true;

                e.Pointer.Capture(this);

                UpdateCaretAndSelectionVisuals();
                RestartCaretBlink();
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!isSelecting)
            {
                return;
            }

            int index;
            if (TryHitTest(e.GetPosition(this), out index))
            {
                caretIndex = index;
                UpdateCaretAndSelectionVisuals();
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (isSelecting)
            {
                isSelecting = false;
                e.Pointer.Capture(null);
            }
        }

        /// <summary>Turns a point in control space into a document caret index.</summary>
        private bool TryHitTest(Point point, out int index)
        {
            index = 0;

            if (visuals.Count == 0)
            {
                return false;
            }

            ParagraphVisual best = null;
            double bestDistance = double.MaxValue;
            Point localPoint = default(Point);

            foreach (ParagraphVisual visual in visuals)
            {
                Point? translated = this.TranslatePoint(point, visual.Text);
                if (translated == null)
                {
                    continue;
                }

                Point candidate = translated.Value;
                double height = visual.Text.Bounds.Height;

                double distance = candidate.Y < 0
                    ? -candidate.Y
                    : (candidate.Y > height ? candidate.Y - height : 0);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = visual;
                    localPoint = candidate;
                }
            }

            if (best == null)
            {
                return false;
            }

            try
            {
                TextHitTestResult hit = best.Text.TextLayout.HitTestPoint(localPoint);

                int offset = hit.TextPosition + (hit.IsTrailing ? 1 : 0);
                offset = Math.Max(0, Math.Min(best.Paragraph.GetLogicalLength(), offset));

                index = best.StartIndex + offset;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MoveCaret(int newIndex, bool extendSelection)
        {
            caretIndex = Math.Max(0, Math.Min(TotalLength(), newIndex));

            if (!extendSelection)
            {
                selectionAnchor = caretIndex;
            }

            UpdateCaretAndSelectionVisuals();
            RestartCaretBlink();
        }

        private void MoveCaretByLine(int direction, bool extendSelection)
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            int target = paragraphIndex + direction;
            if (target < 0 || target >= visuals.Count)
            {
                return;
            }

            Paragraph paragraph = GetParagraph(target);
            if (paragraph == null)
            {
                return;
            }

            int clamped = Math.Min(offset, paragraph.GetLogicalLength());

            MoveCaret(IndexOfParagraphStart(target) + clamped, extendSelection);
        }

        private void MoveCaretToParagraphEdge(bool end, bool extendSelection)
        {
            int paragraphIndex;
            int offset;
            ResolveIndex(caretIndex, out paragraphIndex, out offset);

            Paragraph paragraph = GetParagraph(paragraphIndex);
            if (paragraph == null)
            {
                return;
            }

            int start = IndexOfParagraphStart(paragraphIndex);

            MoveCaret(end ? start + paragraph.GetLogicalLength() : start, extendSelection);
        }

        private async void CopyToClipboard()
        {
            string selected = GetSelectedText();
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.Clipboard == null)
            {
                return;
            }

            try
            {
                await topLevel.Clipboard.SetTextAsync(selected);
            }
            catch
            {
                // Clipboard access can fail on a locked desktop; nothing useful to do.
            }
        }

        private async void PasteFromClipboard()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.Clipboard == null)
            {
                return;
            }

            try
            {
                string text = await ClipboardExtensions.TryGetTextAsync(topLevel.Clipboard);

                if (!string.IsNullOrEmpty(text))
                {
                    InsertText(AcceptsReturn ? text : text.Replace("\r", "").Replace("\n", " "));
                    RestartCaretBlink();
                }
            }
            catch
            {
                // Ignore clipboard failures.
            }
        }

        #endregion
    }
}
