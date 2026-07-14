using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using MyBoard.Model;

namespace MyBoard.Commands
{
    // Represents a single undoable action. Execute() performs it, Undo() reverses it.
    internal interface IUndoableCommand
    {
        void Execute();
        void Undo();
    }
}
