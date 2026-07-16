using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MyBoard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown_Pan;
            PreviewKeyUp += MainWindow_PreviewKeyUp_Pan;
            Closing += MainWindow_Closing;
            Deactivated += MainWindow_Deactivated;

            var viewModel = (MainViewModel)DataContext;

            // Subscribe to the initial board's selection changes
            SubscribeToBoardSelection(viewModel.CurrentBoard);

            // Whenever CurrentBoard changes (navigating in/out of any board), re-subscribe to the NEW board's selection changes
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentBoard))
                {
                    SubscribeToBoardSelection(viewModel.CurrentBoard);
                    SlideSidebarPanel(false);
                }
            };
        }

        // Tracks which board is currently listening to, to unsubscribe cleanly before attaching to a new one 
        private BoardViewModel? subscribedBoard;

        private void SubscribeToBoardSelection(BoardViewModel board)
        {
            if (subscribedBoard != null)
                subscribedBoard.PropertyChanged -= Board_PropertyChanged;

            subscribedBoard = board;
            subscribedBoard.PropertyChanged += Board_PropertyChanged;
        }

        private void Board_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardViewModel.PrimarySelectedItem) && sender is BoardViewModel board)
                SlideSidebarPanel(board.PrimarySelectedItem != null);
        }


        // Space-held pan state — shared across MainWindow.Canvas.cs
        private bool isSpaceHeld;


        // Deletes/Undo/Redo/Cut/Copy/Paste/Duplicate/Rename shortcuts, and Escape for the color popover
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isTyping = Keyboard.FocusedElement is TextBox;
            var board = ((MainViewModel)DataContext).CurrentBoard;
            var viewModel = (MainViewModel)DataContext;

            if (e.Key == Key.Delete && !isTyping)
                board.DeleteSelectedItems();

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                viewModel.SaveBoardCommand.Execute(null);

            if (!isTyping && Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z: viewModel.UndoCommand.Execute(null); break;
                    case Key.Y: viewModel.RedoCommand.Execute(null); break;
                    case Key.C: board.CopySelectedItems(); break;
                    case Key.X: board.CutSelectedItems(); break;
                    case Key.V: board.PasteClipboard(lastCanvasMousePosition.X, lastCanvasMousePosition.Y); break;
                    case Key.D: board.DuplicateSelectedItems(); break;
                }
            }

            if (e.Key == Key.F2 && !isTyping &&
                board.SelectedItems.Count == 1 &&
                board.SelectedItems[0] is BoardViewModel boardToRename)
            {
                boardToRename.BeginEditingTitle();
            }

            if (e.Key == Key.Escape)
            {
                // Only relevant if a board is currently selected and its color tool exists
                if (((FrameworkElement)Content).FindName("BoardColorTool") is Controls.ColorPickerButton colorTool)
                    colorTool.ClosePopover();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((MainViewModel)DataContext).SaveBoardCommand.Execute(null);
        }


        // Walks up the visual tree checking if 'child' is inside 'ancestor' — shared helper
        private static bool IsDescendantOf(DependencyObject? child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return false;
        }


        // Runs BEFORE any other click handling (tunneling) — closes an actively-edited
        // note or board title if the click landed outside it
        private void RootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is not TextBox editBox) return;

            if (editBox.DataContext is NoteItemViewModel note && note.IsEditing)
            {
                if (!IsDescendantOf(e.OriginalSource as DependencyObject, editBox))
                {
                    note.IsEditing = false;
                    Keyboard.Focus(RootGrid);
                }
            }
            else if (editBox.DataContext is BoardViewModel board && board.IsEditingTitle)
            {
                if (!IsDescendantOf(e.OriginalSource as DependencyObject, editBox))
                {
                    board.CommitTitle();
                    Keyboard.Focus(RootGrid);
                }
            }
        }


        // Prevents buttons from retaining focus and hijacking the Space key
        private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is Button)
                Keyboard.Focus(RootGrid);
        }


        private bool isColorPopoverOpen;

        private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
        {
            IsSidebarExpanded = true;
            AnimateSidebarWidth(180);
        }

        private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isColorPopoverOpen)
            {
                IsSidebarExpanded = false;
                AnimateSidebarWidth(60);
            }
        }

        private void AnimateSidebarWidth(double target)
        {
            var animation = new DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            Sidebar.BeginAnimation(WidthProperty, animation);
        }

    }
}