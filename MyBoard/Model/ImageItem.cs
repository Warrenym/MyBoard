using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Model
{
    internal class ImageItem : ICanvasItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 180;
        public double Height { get; set; } = 200;
        public string FilePath { get; set; } = "";
    }
}
