using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class BoardViewModel : ObservableObject, IPositionable
    {
        public Board Model { get; }

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private string title;

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
            _ => throw new NotSupportedException($"Unknown item type: {item.GetType()}")
        };

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnTitleChanged(string value) => Model.Title = value;
    

        // Creates a new NoteItem, adds it to both the Model and the display collection, and places it at a default position so it doesn't overlap existing items.
        public void AddNote()
        {
            var note = new NoteItem
            {
                Content = "New note",
                X = 100,
                Y = 100
            };

            Model.Items.Add(note);              // Keep the Model in sync (needed for saving later)
            Items.Add(new NoteItemViewModel(note)); // Add the wrapped ViewModel so it renders
        }

        // Creates a new sub-Board, adds it the same way notes are added.
        public void AddBoard()
        {
            var board = new Board
            {
                Title = "New Board",
                X = 100,
                Y = 250,
                ParentBoardId = Model.Id // Links the new board back to this one
            };

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


    }
}
    
