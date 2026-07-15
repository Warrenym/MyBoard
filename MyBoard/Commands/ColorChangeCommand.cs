using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Commands
{
    internal class ColorChangeCommand : IUndoableCommand
    {
        private readonly ViewModel.BoardViewModel board;
        private readonly string oldColor;
        private readonly string newColor;

        public ColorChangeCommand(ViewModel.BoardViewModel board, string oldColor, string newColor)
        {
            this.board = board;
            this.oldColor = oldColor;
            this.newColor = newColor;
        }

        public void Execute() => board.Color = newColor;
        public void Undo() => board.Color = oldColor;
    }
}
