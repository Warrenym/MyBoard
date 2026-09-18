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
        private readonly List<(ICanvasItem model, object viewModel, int modelIndex, int viewModelIndex)> positions;

        public DeleteItemsCommand(ObservableCollection<ICanvasItem> modelItems,
                                   ObservableCollection<object> viewModelItems,
                                   List<(ICanvasItem, object)> removedItems)
        {
            this.modelItems = modelItems;
            this.viewModelItems = viewModelItems;
            this.removedItems = removedItems;
            positions = removedItems.Select(pair => (pair.Item1, pair.Item2,
                modelItems.IndexOf(pair.Item1), viewModelItems.IndexOf(pair.Item2))).ToList();
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
            foreach (var entry in positions.OrderBy(p => p.modelIndex))
                modelItems.Insert(entry.modelIndex, entry.model);
            foreach (var entry in positions.OrderBy(p => p.viewModelIndex))
                viewModelItems.Insert(entry.viewModelIndex, entry.viewModel);
        }
    }
}
