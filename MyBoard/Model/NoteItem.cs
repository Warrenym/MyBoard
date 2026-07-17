using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Model
{
    internal class NoteItem : ICanvasItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 180;
        public double Height { get; set; } = 120;

        // Defaults to a single empty Normal block for brand-new notes.
        public NoteDocument Document { get; set; } = NoteDocument.CreateEmpty();

        // Kept temporarily so old save files (which have "Content" but no
        // "Document") don't crash on load — System.Text.Json will populate
        // this from old JSON automatically since the property name matches;
        // MainViewModel's load step converts it into Document once, then this
        // is never written to again going forward.
        public string? Content { get; set; }
    }
}
