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

        // Tracks whether this board's title is currently being edited (TextBox visible).
        // Not persisted — always starts false, except immediately after creation.
        [ObservableProperty]
        private bool isEditingTitle;

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
        public BoardViewModel AddBoard()
        {
            var board = new Board { Title = "Unnamed Board", X = 100, Y = 250, ParentBoardId = Model.Id };
            Model.Items.Add(board);

            var boardViewModel = new BoardViewModel(board);
            Items.Add(boardViewModel);
            boardViewModel.IsEditingTitle = true;

            return boardViewModel;
        }

        // Exits title-editing mode. If the user left it blank, defaults to "Unnamed Board"
        public void CommitTitle()
        {
            if (string.IsNullOrWhiteSpace(Title))
                Title = "Unnamed Board";
            IsEditingTitle = false;
        }

        // Creates a new ImageItem at the given position — used by drag-and-drop.
        public void AddImage(string filePath, double x, double y)
        {
            var image = new ImageItem { FilePath = filePath, X = x, Y = y };
            Model.Items.Add(image);
            Items.Add(new ImageItemViewModel(image));
        }

        // Tracks every currently selected item on this board (supports multi-select).
        // Kept in sync with each item's own IsSelected flag, which drives the highlight border.
        public ObservableCollection<object> SelectedItems { get; } = new();

        // Selects a single item, replacing any existing selection.
        // Used for a normal single click.
        public void SelectItem(object item)
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();

            if (item is ISelectable selectable)
                selectable.IsSelected = true;

            SelectedItems.Add(item);
        }

        // Selects a whole set of items at once, replacing any existing selection.
        // Used by the drag-box multi-select.
        public void SelectItems(IEnumerable<object> items)
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();

            foreach (var item in items)
            {
                if (item is ISelectable selectable)
                    selectable.IsSelected = true;
                SelectedItems.Add(item);
            }
        }

        // Deselects everything — called when clicking empty canvas space
        public void ClearSelection()
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();
        }

        // Removes every currently selected item from both the display collection
        // and the underlying Model — now handles multiple items at once
        public void DeleteSelectedItems()
        {
            foreach (var item in SelectedItems.ToList()) // ToList() avoids mutating while iterating
            {
                if (item is ICanvasItemViewModel canvasItemVm)
                    Model.Items.Remove(canvasItemVm.Model);
                Items.Remove(item);
            }
            SelectedItems.Clear();
        }



    }
}
    
