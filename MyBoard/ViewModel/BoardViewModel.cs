using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class BoardViewModel : ObservableObject, IPositionable, ISelectable, ICanvasItemViewModel
    {
        public Board Model { get; }

        ICanvasItem ICanvasItemViewModel.Model => Model;

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private bool isSelected;

        // Tracks which item is currently selected on this board, if any
        [ObservableProperty]
        private object? selectedItem;

        public ObservableCollection<object> Items { get; } = new();

        public BoardViewModel(Board model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            title = model.Title;

            foreach (var item in model.Items)
                Items.Add(WrapModel(item));
        }

        private object WrapModel(ICanvasItem item) => item switch
        {
            NoteItem note => new NoteItemViewModel(note),
            ImageItem image => new ImageItemViewModel(image),
            Board board => new BoardViewModel(board),
            _ => throw new System.NotSupportedException($"Unknown item type: {item.GetType()}")
        };

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnTitleChanged(string value) => Model.Title = value;

        // Creates a new NoteItem, adds it to both the Model and the display collection,
        // and places it at a default position so it doesn't overlap existing items.
        public void AddNote()
        {
            var note = new NoteItem { Content = "New note", X = 100, Y = 100 };
            Model.Items.Add(note);
            Items.Add(new NoteItemViewModel(note));
        }

        // Creates a new sub-Board, adds it the same way notes are added.
        public void AddBoard()
        {
            var board = new Board { Title = "New Board", X = 100, Y = 250, ParentBoardId = Model.Id };
            Model.Items.Add(board);
            Items.Add(new BoardViewModel(board));
        }

        // Creates a new ImageItem at the given position — used by drag-and-drop.
        public void AddImage(string filePath, double x, double y)
        {
            var image = new ImageItem { FilePath = filePath, X = x, Y = y };
            Model.Items.Add(image);
            Items.Add(new ImageItemViewModel(image));
        }

        // Selects the given item and deselects everything else on this board.
        public void SelectItem(object item)
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;

            if (item is ISelectable selectable)
                selectable.IsSelected = true;

            SelectedItem = item;
        }

        // Deselects everything — called when clicking empty canvas space
        public void ClearSelection()
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;

            SelectedItem = null;
        }

        // Removes the currently selected item from both the display collection
        // and the underlying Model, so the deletion persists once save/load exists
        public void DeleteSelectedItem()
        {
            if (SelectedItem is ICanvasItemViewModel canvasItemVm)
                Model.Items.Remove(canvasItemVm.Model);

            if (SelectedItem != null)
                Items.Remove(SelectedItem);

            SelectedItem = null;
        }


    }
}
    
