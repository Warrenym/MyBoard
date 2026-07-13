using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBoard.Model;
using MyBoard.Services;
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

        [ObservableProperty]
        private double panX;

        [ObservableProperty]
        private double panY;

        // Human-readable zoom percentage, e.g. "150%" — kept in sync automatically
        // whenever ZoomLevel changes, so the UI has a ready-to-display string.
        [ObservableProperty]
        private double zoomLevel = 1.0;

        [ObservableProperty]
        private string zoomDisplayText = "100%";

        partial void OnZoomLevelChanged(double value)
        {
            ZoomDisplayText = $"{value * 100:0}%";
        }

        //The navigation trail
        public ObservableCollection<BoardViewModel> BreadcrumbTrail { get; } = new();

        public MainViewModel()
        {
            // Try loading a previously saved board tree; fall back to a fresh "Home" board only if this is the first run (no save file yet)
            Board homeBoard = BoardSaveService.Load() ?? new Board { Title = "Home" };

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

        // Called when a breadcrumb item is clicked — jumps back to that level, discarding everything deeper in the trail
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

        // Manually triggered save (e.g. Ctrl+S) — saves from the root Home board downward, regardless of which nested board is currently being viewed
        [RelayCommand]
        private void SaveBoard()
        {
            var rootBoard = BreadcrumbTrail[0].Model; // BreadcrumbTrail[0] is always Home
            BoardSaveService.Save(rootBoard);
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
            var newBoard = CurrentBoard.AddBoard();
            CurrentBoard.SelectItem(newBoard); // Highlights it immediately, matching File Explorer's new-folder behavior
        }
    }
}