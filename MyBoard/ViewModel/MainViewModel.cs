using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private BoardViewModel currentBoard; // The board currently shown on the canvas

        //The navigation trail
        public ObservableCollection<BoardViewModel> BreadcrumbTrail { get; } = new();

        public MainViewModel()
        {
            var homeBoard = new Board { Title = "Home" };

            var welcomeNote = new NoteItem { Content = "Welcome to your board!", X = 100, Y = 100 };

            var subBoard = new Board { Title = "Drawing", X = 300, Y = 100 };
            subBoard.Items.Add(new NoteItem { Content = "Inside Drawing board", X = 50, Y = 50 });

            homeBoard.Items.Add(welcomeNote);
            homeBoard.Items.Add(subBoard);

            currentBoard = new BoardViewModel(homeBoard);
            BreadcrumbTrail.Add(currentBoard); // Start the trail at Home
        }

        // Called when a board tile is clicked — moves navigation one level deeper
        [RelayCommand]
        private void NavigateToBoard(BoardViewModel board)
        {
            CurrentBoard = board;
            BreadcrumbTrail.Add(board);
        }

        // Called when a breadcrumb item is clicked — jumps back to that level,
        // discarding everything deeper in the trail
        [RelayCommand]
        private void NavigateToBreadcrumb(BoardViewModel board)
        {
            int index = BreadcrumbTrail.IndexOf(board);
            if (index == -1) return;

            // Remove everything after the clicked board
            while (BreadcrumbTrail.Count > index + 1)
                BreadcrumbTrail.RemoveAt(BreadcrumbTrail.Count - 1);

            CurrentBoard = board;
        }

        // Adds a new note to whichever board is currently displayed
        [RelayCommand]
        private void AddNote()
        {
            CurrentBoard.AddNote();
        }

        // Adds a new sub-board to whichever board is currently displayed
        [RelayCommand]
        private void AddBoard()
        {
            CurrentBoard.AddBoard();
        }
    }
}
