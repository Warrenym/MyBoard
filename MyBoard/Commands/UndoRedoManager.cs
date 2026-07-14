using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using MyBoard.Model;

namespace MyBoard.Commands
{
    internal partial class UndoRedoManager : ObservableObject
    {
        private readonly Stack<IUndoableCommand> undoStack = new();
        private readonly Stack<IUndoableCommand> redoStack = new();

        [ObservableProperty]
        private bool canUndo;

        [ObservableProperty]
        private bool canRedo;

        // Executes a NEW action and records it — use this for actions that haven't happened yet (e.g. clicking "Add Note")
        public void Do(IUndoableCommand command)
        {
            command.Execute();
            Record(command);
        }

        // Records an action that has ALREADY happened (e.g. a drag that already moved an item live on screen) — skips calling Execute() again
        public void Record(IUndoableCommand command)
        {
            undoStack.Push(command);
            redoStack.Clear(); // A new action invalidates any old redo history
            RefreshFlags();
        }

        public void Undo()
        {
            if (undoStack.Count == 0) return;
            var command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);
            RefreshFlags();
        }

        public void Redo()
        {
            if (redoStack.Count == 0) return;
            var command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);
            RefreshFlags();
        }

        private void RefreshFlags()
        {
            CanUndo = undoStack.Count > 0;
            CanRedo = redoStack.Count > 0;
        }
    }
}
