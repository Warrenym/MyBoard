using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MyBoard.Commands
{
    // Handles undo/redo for adding one item to a board — 
    // reused for AddNote, AddBoard, AddImage, Paste, and Duplicate, since they're all fundamentally "insert this item into these two collections."
    internal class AddItemCommand : IUndoableCommand
    {
        private readonly ObservableCollection<ICanvasItem> modelItems;
        private readonly ObservableCollection<object> viewModelItems;
        private readonly ICanvasItem model;
        private readonly object viewModel;

        public AddItemCommand(ObservableCollection<ICanvasItem> modelItems,
                               ObservableCollection<object> viewModelItems,
                               ICanvasItem model, object viewModel)
        {
            this.modelItems = modelItems;
            this.viewModelItems = viewModelItems;
            this.model = model;
            this.viewModel = viewModel;
        }

        public void Execute()
        {
            modelItems.Add(model);
            viewModelItems.Add(viewModel);
        }

        public void Undo()
        {
            modelItems.Remove(model);
            viewModelItems.Remove(viewModel);
        }
    }
}
