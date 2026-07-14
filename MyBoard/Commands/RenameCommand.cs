using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using MyBoard.Model;

namespace MyBoard.Commands
{
    internal class RenameCommand : IUndoableCommand
    {
        private readonly ViewModel.BoardViewModel board;
        private readonly string oldTitle;
        private readonly string newTitle;

        public RenameCommand(ViewModel.BoardViewModel board, string oldTitle, string newTitle)
        {
            this.board = board;
            this.oldTitle = oldTitle;
            this.newTitle = newTitle;
        }

        public void Execute() => board.Title = newTitle;
        public void Undo() => board.Title = oldTitle;
    }
}
