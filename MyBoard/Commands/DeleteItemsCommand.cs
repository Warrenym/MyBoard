using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using MyBoard.Model;

namespace MyBoard.Commands
{
    internal class DeleteItemsCommand : IUndoableCommand
    {
        private readonly ObservableCollection<ICanvasItem> modelItems;
        private readonly ObservableCollection<object> viewModelItems;
        // Stored as a list of pairs so undo can re-insert each Model alongside its ViewModel
        private readonly List<(ICanvasItem model, object viewModel)> removedItems;

        public DeleteItemsCommand(ObservableCollection<ICanvasItem> modelItems,
                                   ObservableCollection<object> viewModelItems,
                                   List<(ICanvasItem, object)> removedItems)
        {
            this.modelItems = modelItems;
            this.viewModelItems = viewModelItems;
            this.removedItems = removedItems;
        }

        public void Execute()
        {
            foreach (var (model, viewModel) in removedItems)
            {
                modelItems.Remove(model);
                viewModelItems.Remove(viewModel);
            }
        }

        public void Undo()
        {
            foreach (var (model, viewModel) in removedItems)
            {
                modelItems.Add(model);
                viewModelItems.Add(viewModel);
            }
        }
    }
}
