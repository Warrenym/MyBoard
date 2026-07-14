using MyBoard.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Commands
{
    internal class MoveItemsCommand : IUndoableCommand
    {
        private readonly List<(IPositionable item, double oldX, double oldY, double newX, double newY)> moves;

        public MoveItemsCommand(List<(IPositionable, double, double, double, double)> moves)
        {
            this.moves = moves;
        }

        public void Execute()
        {
            foreach (var (item, _, _, newX, newY) in moves)
            {
                item.X = newX;
                item.Y = newY;
            }
        }

        public void Undo()
        {
            foreach (var (item, oldX, oldY, _, _) in moves)
            {
                item.X = oldX;
                item.Y = oldY;
            }
        }
    }
}
