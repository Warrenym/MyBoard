using MyBoard.Model;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MyBoard.Services
{
    // The ONLY file in this feature that knows about WPF's FlowDocument.
    // Everything else (NoteDocument, NoteBlock, NoteRun) is plain, portable data — if this app ever moves to another UI framework, 
    // only this converter needs replacing; the data model and ViewModels stay as-is.
    public static class NoteDocumentConverter
    {

        // ---------- NoteDocument -> FlowDocument (for display/editing) ----------

        public static FlowDocument ToFlowDocument(NoteDocument document)
        {
            var flowDoc = new FlowDocument();

            foreach (var block in document.Blocks)
                flowDoc.Blocks.Add(BuildParagraph(block));

            return flowDoc;
        }


        private static Paragraph BuildParagraph(NoteBlock block)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            ApplyBlockStyle(paragraph, block.Type);

            foreach (var run in block.Runs)
                paragraph.Inlines.Add(BuildRun(run));

            // Blocks with no runs yet (a brand-new empty line) still need
            // SOME inline, or WPF won't render an empty line with the
            // correct height/cursor position
            if (block.Runs.Count == 0)
                paragraph.Inlines.Add(new Run(""));

            return paragraph;
        }


        private static Run BuildRun(NoteRun run)
        {
            var r = new Run(run.Text)
            {
                FontWeight = run.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = run.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = NoteColorPalette.GetTextBrush(run.TextColorKey)
            };

            if (run.InlineCode)
                r.FontFamily = new FontFamily("Consolas");

            var decorations = new TextDecorationCollection();
            if (run.Strikethrough) decorations.Add(TextDecorations.Strikethrough[0]);
            if (run.Underline) decorations.Add(TextDecorations.Underline[0]);
            if (decorations.Count > 0) r.TextDecorations = decorations;

            if (run.HighlightColorKey != null && run.HighlightColorKey != "none")
                r.Background = NoteColorPalette.GetHighlightBrush(run.HighlightColorKey);

            return r;
        }


        // Exposes the existing private styling logic so ApplyBlockType (and later, the Enter-key reset logic) can reuse it without duplicating the switch statement
        public static void ApplyBlockStylePublic(Paragraph paragraph, NoteBlockType type) =>
            ApplyBlockStyle(paragraph, type);

        public static NoteBlockType DetectBlockTypePublic(Paragraph paragraph) => DetectBlockType(paragraph);

        // Applies block-level visual treatment — 
        // font size/weight for headings, monospace+background for code, left-border+italic for quotes
        private static void ApplyBlockStyle(Paragraph paragraph, NoteBlockType type)
        {
            switch (type)
            {
                case NoteBlockType.LargeHeading:
                    paragraph.FontSize = 22;
                    paragraph.FontWeight = FontWeights.Bold;
                    break;
                case NoteBlockType.NormalHeading:
                    paragraph.FontSize = 17;
                    paragraph.FontWeight = FontWeights.Bold;
                    break;
                case NoteBlockType.SmallText:
                    paragraph.FontSize = 11;
                    break;
                case NoteBlockType.CodeBlock:
                    paragraph.FontFamily = new FontFamily("Consolas");
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
                    paragraph.Padding = new Thickness(8, 4, 8, 4);
                    break;
                case NoteBlockType.QuoteBlock:
                    paragraph.FontStyle = FontStyles.Italic;
                    paragraph.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                    paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
                    paragraph.Padding = new Thickness(8, 0, 0, 0);
                    break;
                    // Normal: no overrides, uses the RichTextBox's default font/size
            }

        }


        // Applies a block type to whichever paragraph the caret is currently in.
        // Used by the Text Style popover — changing "this line" to Large Heading, etc.
        public static void ApplyBlockType(RichTextBox rtb, NoteBlockType type)
        {
            var paragraph = rtb.CaretPosition.Paragraph;
            if (paragraph == null) return;

            var previousType = DetectBlockType(paragraph);

            // Reset any prior block-level overrides before applying the new ones, so switching FROM CodeBlock TO Normal doesn't leave stray formatting behind
            paragraph.ClearValue(Paragraph.FontFamilyProperty);
            paragraph.ClearValue(Paragraph.FontSizeProperty);
            paragraph.ClearValue(Paragraph.FontWeightProperty);
            paragraph.ClearValue(Paragraph.FontStyleProperty);
            paragraph.ClearValue(Paragraph.BackgroundProperty);
            paragraph.ClearValue(Paragraph.BorderBrushProperty);
            paragraph.ClearValue(Paragraph.BorderThicknessProperty);
            paragraph.ClearValue(Paragraph.PaddingProperty);

            ApplyBlockStylePublic(paragraph, type);

            // Add/remove the wrapping quotation marks as the block type transitions in or out of QuoteBlock — text in between is left completely alone
            if (type == NoteBlockType.QuoteBlock && previousType != NoteBlockType.QuoteBlock)
                WrapParagraphInQuotes(paragraph);
            else if (type != NoteBlockType.QuoteBlock && previousType == NoteBlockType.QuoteBlock)
                UnwrapParagraphQuotes(paragraph);
        }


        // Wraps a paragraph's content with non-editable opening/closing quote marks
        // (InlineUIContainer — atomic, caret can sit beside them but never inside them, so they can't be typed into or split like normal characters)
        private static void WrapParagraphInQuotes(Paragraph paragraph)
        {
            var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
            string existingText = range.Text.TrimEnd('\r', '\n');

            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(new InlineUIContainer(MakeQuoteGlyph("\u201C"))); // “
            paragraph.Inlines.Add(new Run(existingText));
            paragraph.Inlines.Add(new InlineUIContainer(MakeQuoteGlyph("\u201D"))); // ”
        }

        private static void UnwrapParagraphQuotes(Paragraph paragraph)
        {
            // Extract only the actual typed text, skipping the quote-glyph containers
            var text = new System.Text.StringBuilder();
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is Run run) text.Append(run.Text);
            }

            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(new Run(text.ToString()));
        }

        private static TextBlock MakeQuoteGlyph(string symbol) => new()
        {
            Text = symbol,
            FontStyle = FontStyles.Italic,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            Focusable = false,
            IsHitTestVisible = false // clicks pass through to the text beside it, not the glyph itself
        };



        // ---------- FlowDocument -> NoteDocument (for saving) ----------

        public static NoteDocument ToNoteDocument(FlowDocument flowDoc)
        {
            var document = new NoteDocument { Blocks = new List<NoteBlock>() };

            foreach (var block in flowDoc.Blocks)
            {
                if (block is not Paragraph paragraph) continue;
                document.Blocks.Add(ExtractBlock(paragraph));
            }

            if (document.Blocks.Count == 0)
                document.Blocks.Add(new NoteBlock { Type = NoteBlockType.Normal });

            return document;
        }

        private static NoteBlock ExtractBlock(Paragraph paragraph)
        {
            var block = new NoteBlock
            {
                Type = DetectBlockType(paragraph),
                Runs = new List<NoteRun>()
            };

            foreach (var inline in paragraph.Inlines)
            {
                if (inline is not Run run) continue;
                if (string.IsNullOrEmpty(run.Text)) continue; // skip the empty placeholder Run

                block.Runs.Add(new NoteRun
                {
                    Text = run.Text,
                    Bold = run.FontWeight == FontWeights.Bold,
                    Italic = run.FontStyle == FontStyles.Italic,
                    Strikethrough = run.TextDecorations?.Contains(TextDecorations.Strikethrough[0]) ?? false,
                    Underline = run.TextDecorations?.Contains(TextDecorations.Underline[0]) ?? false,
                    InlineCode = run.FontFamily?.Source == "Consolas" && paragraph.FontFamily?.Source != "Consolas"
                });
            }

            return block;
        }

        // Reverse-maps a Paragraph's visual properties back to a block type —
        // works because ApplyBlockStyle above sets a distinct combination of properties for each type
        private static NoteBlockType DetectBlockType(Paragraph paragraph)
        {
            if (paragraph.FontFamily?.Source == "Consolas") return NoteBlockType.CodeBlock;
            if (paragraph.FontStyle == FontStyles.Italic && paragraph.BorderThickness.Left > 0) return NoteBlockType.QuoteBlock;
            if (paragraph.FontSize >= 22) return NoteBlockType.LargeHeading;
            if (paragraph.FontSize >= 17 && paragraph.FontWeight == FontWeights.Bold) return NoteBlockType.NormalHeading;
            if (paragraph.FontSize > 0 && paragraph.FontSize <= 11) return NoteBlockType.SmallText;
            return NoteBlockType.Normal;
        }
    }
}