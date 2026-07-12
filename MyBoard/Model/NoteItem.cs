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
        public string Content { get; set; } = "";
    }
}
