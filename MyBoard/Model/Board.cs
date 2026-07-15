using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MyBoard.Model
{
    internal class Board : ICanvasItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public double X { get; set; }
        public double Y { get; set; }
        public string Title { get; set; } = "Untitled Board";
        public string Color { get; set; } = "#B39DDB";
        public Guid? ParentBoardId { get; set; }
        public ObservableCollection<ICanvasItem> Items { get; set; } = new();
    }
}
