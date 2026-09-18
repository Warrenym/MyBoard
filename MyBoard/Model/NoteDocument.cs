using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Model
{
    // The block-level style — determines how a whole line/paragraph looks and behaves (e.g. Enter resets non-Normal blocks back to Normal).
    public enum NoteBlockType
    {
        Normal,
        LargeHeading,
        NormalHeading,
        SmallText,
        CodeBlock,
        QuoteBlock
    }

    public enum NoteListType
    {
        Bullet,
        Numbered,
        Checkbox
    }

    // A single span of text with its own formatting — multiple runs make up one block, e.g. "This is " (plain) + "bold" (bold) + " text" (plain)
    public class NoteRun
    {
        public string Text { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Strikethrough { get; set; }
        public bool Underline { get; set; }
        public bool InlineCode { get; set; }

        // Fixed palette only, per your decision — these store a palette KEY (e.g. "red", "blue"), not a hex string, since the swatches are fixed
        
        public string? TextColorKey { get; set; }
        public string? HighlightColorKey { get; set; }

        public string? LinkUrl { get; set; }
    }

    // One line/paragraph — a type (heading, quote, etc.) plus its runs
    public class NoteBlock
    {
        public NoteBlockType Type { get; set; } = NoteBlockType.Normal;
        public List<NoteRun> Runs { get; set; } = new();

        public NoteListType? ListType { get; set; }
        public bool IsChecked { get; set; }

        // Kept for compatibility with notes saved by older builds.
        public int? ListIndex { get; set; }
    }

    // The full note — just an ordered list of blocks. Plain data, JSON-serializable with System.Text.Json exactly like Board/NoteItem already are — 
    // no WPF types anywhere in this file, which is what keeps it portable later.
    public class NoteDocument
    {
        public List<NoteBlock> Blocks { get; set; } = new();

        // Convenience factory for a brand-new, empty note
        public static NoteDocument CreateEmpty() => new()
        {
            Blocks = new List<NoteBlock>
            {
                new NoteBlock { Type = NoteBlockType.Normal, Runs = new List<NoteRun>() }
            }
        };
    }
}
