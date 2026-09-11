using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WLMClient.Compat.Documents
{
    /// <summary>How a line box is sized. Kept for source compatibility; behaviour follows WPF.</summary>
    public enum LineStackingStrategy
    {
        MaxHeight = 0,
        BlockLineHeight = 1
    }

    /// <summary>Base type for anything that can sit inside a paragraph.</summary>
    public abstract class Inline
    {
    }

    /// <summary>A stretch of plain text.</summary>
    public class Run : Inline
    {
        public string Text { get; set; }

        public Run()
        {
            Text = string.Empty;
        }

        public Run(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    /// <summary>A control (typically an emoticon image or a link) embedded in the text flow.</summary>
    public class InlineUIContainer : Inline
    {
        public Control Child { get; set; }
        public BaselineAlignment BaselineAlignment { get; set; }

        public InlineUIContainer()
        {
            BaselineAlignment = BaselineAlignment.Baseline;
        }

        public InlineUIContainer(Control child) : this()
        {
            Child = child;
        }
    }

    /// <summary>Ordered collection of the inlines making up a paragraph.</summary>
    public class InlineCollection : IEnumerable<Inline>
    {
        private readonly List<Inline> items = new List<Inline>();
        private readonly Paragraph owner;

        internal InlineCollection(Paragraph owner)
        {
            this.owner = owner;
        }

        public int Count
        {
            get { return items.Count; }
        }

        public Inline this[int index]
        {
            get { return items[index]; }
            set { items[index] = value; owner.NotifyChanged(); }
        }

        /// <summary>Adds plain text, matching WPF's implicit string to Run conversion.</summary>
        public void Add(string text)
        {
            items.Add(new Run(text));
            owner.NotifyChanged();
        }

        public void Add(Inline inline)
        {
            items.Add(inline);
            owner.NotifyChanged();
        }

        public void Insert(int index, Inline inline)
        {
            items.Insert(index, inline);
            owner.NotifyChanged();
        }

        public void RemoveAt(int index)
        {
            items.RemoveAt(index);
            owner.NotifyChanged();
        }

        public void Remove(Inline inline)
        {
            items.Remove(inline);
            owner.NotifyChanged();
        }

        public void Clear()
        {
            items.Clear();
            owner.NotifyChanged();
        }

        public int IndexOf(Inline inline)
        {
            return items.IndexOf(inline);
        }

        /// <summary>Replaces a single inline with a sequence, used when expanding emoticons.</summary>
        public void ReplaceRange(int index, int count, IEnumerable<Inline> replacement)
        {
            items.RemoveRange(index, count);
            items.InsertRange(index, replacement);
            owner.NotifyChanged();
        }

        public IEnumerator<Inline> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>Base type for the top level pieces of a document.</summary>
    public abstract class Block
    {
        internal FlowDocument Owner { get; set; }

        internal void NotifyChanged()
        {
            if (Owner != null)
            {
                Owner.NotifyChanged();
            }
        }
    }

    /// <summary>A block of inline content with its own spacing, colour and alignment.</summary>
    public class Paragraph : Block
    {
        public InlineCollection Inlines { get; private set; }

        public Thickness Margin { get; set; }
        public Thickness Padding { get; set; }
        public Thickness BorderThickness { get; set; }
        public IBrush BorderBrush { get; set; }
        public IBrush Foreground { get; set; }
        public double FontSize { get; set; }
        public FontWeight FontWeight { get; set; }
        public FontFamily FontFamily { get; set; }
        public TextAlignment TextAlignment { get; set; }
        public double LineHeight { get; set; }
        public LineStackingStrategy LineStackingStrategy { get; set; }

        public Paragraph()
        {
            Inlines = new InlineCollection(this);
            FontSize = double.NaN;
            LineHeight = double.NaN;
            FontWeight = FontWeight.Normal;
            TextAlignment = TextAlignment.Left;
        }

        public Paragraph(Inline inline) : this()
        {
            Inlines.Add(inline);
        }

        /// <summary>The paragraph's text with each embedded control counted as one character.</summary>
        public string GetLogicalText()
        {
            StringBuilder builder = new StringBuilder();

            foreach (Inline inline in Inlines)
            {
                Run run = inline as Run;
                if (run != null)
                {
                    builder.Append(run.Text);
                }
                else
                {
                    builder.Append('￼');
                }
            }

            return builder.ToString();
        }

        public int GetLogicalLength()
        {
            int length = 0;

            foreach (Inline inline in Inlines)
            {
                Run run = inline as Run;
                length += run != null ? run.Text.Length : 1;
            }

            return length;
        }
    }

    /// <summary>Ordered collection of the blocks in a document.</summary>
    public class BlockCollection : IEnumerable<Block>
    {
        private readonly List<Block> items = new List<Block>();
        private readonly FlowDocument owner;

        internal BlockCollection(FlowDocument owner)
        {
            this.owner = owner;
        }

        public int Count
        {
            get { return items.Count; }
        }

        public Block this[int index]
        {
            get { return items[index]; }
        }

        public void Add(Block block)
        {
            block.Owner = owner;
            items.Add(block);
            owner.NotifyChanged();
        }

        public void Insert(int index, Block block)
        {
            block.Owner = owner;
            items.Insert(index, block);
            owner.NotifyChanged();
        }

        public void Remove(Block block)
        {
            items.Remove(block);
            owner.NotifyChanged();
        }

        public void RemoveAt(int index)
        {
            items.RemoveAt(index);
            owner.NotifyChanged();
        }

        public void Clear()
        {
            items.Clear();
            owner.NotifyChanged();
        }

        public IEnumerator<Block> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// The content of a <see cref="RichTextBox"/>: an ordered list of paragraphs. Raises
    /// <see cref="Changed"/> so the owning control can re-render.
    /// </summary>
    public class FlowDocument
    {
        public BlockCollection Blocks { get; private set; }

        /// <summary>Raised whenever the document structure or content changes.</summary>
        public event EventHandler Changed;

        public FlowDocument()
        {
            Blocks = new BlockCollection(this);
        }

        internal void NotifyChanged()
        {
            EventHandler handler = Changed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>All paragraphs in order; blocks of other kinds are skipped.</summary>
        public IEnumerable<Paragraph> Paragraphs
        {
            get { return Blocks.OfType<Paragraph>(); }
        }

        public bool IsEmpty
        {
            get
            {
                foreach (Paragraph paragraph in Paragraphs)
                {
                    if (paragraph.GetLogicalLength() > 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
