using MyBoard.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Commands
{
    class ResizeItemCommand : IUndoableCommand
    {
        private readonly IResizable item;
        private readonly double oldWidth, oldHeight, newWidth, newHeight;

        public ResizeItemCommand(IResizable item, double oldWidth, double oldHeight, double newWidth, double newHeight)
        {
            this.item = item;
            this.oldWidth = oldWidth;
            this.oldHeight = oldHeight;
            this.newWidth = newWidth;
            this.newHeight = newHeight;
        }

        public void Execute() { item.Width = newWidth; item.Height = newHeight; }
        public void Undo() { item.Width = oldWidth; item.Height = oldHeight; }
    }
}
