using MyBoard.ViewModel;
using Microsoft.Win32;
using MyBoard.Services;
using System.ComponentModel;
using System.IO;
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
            AttachViewModel(new MainViewModel());

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown_Pan;
            PreviewKeyUp += MainWindow_PreviewKeyUp_Pan;
            Closing += MainWindow_Closing;
            Deactivated += MainWindow_Deactivated;
            Activated += MainWindow_Activated;
        }

        // Tracks which board is currently listening to, to unsubscribe cleanly before attaching to a new one
        private BoardViewModel? subscribedBoard;
        private MainViewModel? attachedViewModel;
        private bool isStoragePromptOpen;

        private void AttachViewModel(MainViewModel viewModel)
        {
            if (attachedViewModel != null)
                attachedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            if (subscribedBoard != null)
                subscribedBoard.PropertyChanged -= Board_PropertyChanged;

            attachedViewModel = viewModel;
            DataContext = viewModel;
            SubscribeToBoardSelection(viewModel.CurrentBoard);
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            SlideSidebarPanel(false);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentBoard) && sender is MainViewModel viewModel)
            {
                SubscribeToBoardSelection(viewModel.CurrentBoard);
                SlideSidebarPanel(false);
            }
        }

        private void SubscribeToBoardSelection(BoardViewModel board)
        {
            if (subscribedBoard != null)
                subscribedBoard.PropertyChanged -= Board_PropertyChanged;

            subscribedBoard = board;
            subscribedBoard.PropertyChanged += Board_PropertyChanged;
        }

        private void Board_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardViewModel.PrimarySelectedItem) && sender is BoardViewModel board)
            {
                SlideSidebarPanel(board.PrimarySelectedItem != null);

                // Point the formatting toolbar at whichever note is now selected —
                // fixes a bug where the LAST note to ever load on the canvas would
                // silently stay the format target forever, regardless of what's
                // actually selected
                activeFormattingTarget = board.PrimarySelectedItem is NoteItemViewModel note
                    ? FindNoteRichTextBox(note)
                    : null;
            }
        }


        // Space-held pan state — shared across MainWindow.Canvas.cs
        private bool isSpaceHeld;


        // Deletes/Undo/Redo/Cut/Copy/Paste/Duplicate/Rename shortcuts, and Escape for the color popover
        private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            bool isTyping = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is RichTextBox;
            bool canvasShortcut = Services.EditingPolicy.CanHandleCanvasShortcut(isTyping, e.Handled);
            var board = ((MainViewModel)DataContext).CurrentBoard;
            var viewModel = (MainViewModel)DataContext;

            if (e.Key == Key.Delete && canvasShortcut)
            {
                board.DeleteSelectedItems();
                e.Handled = true;
            }

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TrySave(viewModel);
                e.Handled = true;
            }

            if (canvasShortcut && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (Services.EditingPolicy.IsCanvasControlShortcut(e.Key)) e.Handled = true;
                switch (e.Key)
                {
                    case Key.Z: viewModel.UndoCommand.Execute(null); break;
                    case Key.Y: viewModel.RedoCommand.Execute(null); break;
                    case Key.C: board.CopySelectedItems(); break;
                    case Key.X: board.CutSelectedItems(); break;
                    case Key.V:
                        await PasteAtCanvasPositionAsync(lastCanvasMousePosition);
                        e.Handled = true;
                        break;
                    case Key.D: board.DuplicateSelectedItems(); break;
                }
            }

            if (e.Key == Key.F2 && canvasShortcut &&
                Services.EditingPolicy.CanRename(board.SelectedItems.Count, board.PrimarySelectedItem is BoardViewModel) &&
                board.SelectedItems[0] is BoardViewModel boardToRename)
            {
                boardToRename.BeginEditingTitle();
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                if (((FrameworkElement)Content).FindName("BoardColorTool") is Controls.ColorPickerButton colorTool)
                    colorTool.ClosePopover();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!TrySave((MainViewModel)DataContext)) e.Cancel = true;
        }

        private bool TrySave(MainViewModel viewModel)
        {
            try
            {
                viewModel.SaveNow();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"MyBoard could not save to:\n{viewModel.StorageFolder}\n\n{ex.Message}",
                    "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ChangeDataLocation_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            var dialog = new OpenFolderDialog
            {
                Title = "Choose the folder that stores MyBoard data",
                InitialDirectory = viewModel.StorageFolder,
                Multiselect = false
            };

            isStoragePromptOpen = true;
            try
            {
                if (dialog.ShowDialog(this) != true) return;
                string selectedFolder = Path.GetFullPath(dialog.FolderName)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(selectedFolder, viewModel.StorageFolder,
                    StringComparison.OrdinalIgnoreCase))
                {
                    AppStoragePaths.SelectRootFolder(selectedFolder);
                    MessageBox.Show(this, "This folder is now saved as this computer's explicit MyBoard data location.",
                        "Data location saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!TrySave(viewModel)) return;
                string selectedBoard = Path.Combine(selectedFolder, "board.json");
                if (File.Exists(selectedBoard))
                {
                    _ = new BoardSaveService(selectedFolder).Load();
                    var answer = MessageBox.Show(this,
                        "This folder already contains a MyBoard board. Switch to it and replace the board currently shown in the app?",
                        "Use existing MyBoard data", MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.No);
                    if (answer != MessageBoxResult.Yes) return;
                }
                else
                {
                    AppStoragePaths.CopyDataIfMissing(viewModel.StorageFolder, selectedFolder);
                }

                AppStoragePaths.SelectRootFolder(selectedFolder);
                AttachViewModel(new MainViewModel());
                MessageBox.Show(this,
                    $"MyBoard now stores its data in:\n{selectedFolder}\n\nChoose this same synced folder on your other computer.",
                    "Data location changed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"The data location could not be changed.\n\n{ex.Message}",
                    "Data location unchanged", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isStoragePromptOpen = false;
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (isStoragePromptOpen || DataContext is not MainViewModel viewModel) return;

            try
            {
                if (!viewModel.HasExternalBoardChanges()) return;
                isStoragePromptOpen = true;
                var answer = MessageBox.Show(this,
                    "The board changed in the data folder, probably because it was synced from another computer. Reload it now?\n\nChoosing No keeps the current view, but saving remains blocked to protect the newer file.",
                    "Synced board changed", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.Yes);
                if (answer == MessageBoxResult.Yes)
                    AttachViewModel(new MainViewModel());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"MyBoard could not check the synced board.\n\n{ex.Message}",
                    "Sync check failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isStoragePromptOpen = false;
            }
        }


        // Walks up the visual tree checking if 'child' is inside 'ancestor' — shared helper
        private static bool IsDescendantOf(DependencyObject? child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;

                child = child is System.Windows.Media.Visual || child is System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(child)
                    : LogicalTreeHelper.GetParent(child);
            }
            return false;
        }

        private static bool IsLogicalDescendantOf(DependencyObject? child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;
                child = LogicalTreeHelper.GetParent(child);
            }
            return false;
        }


        // Runs BEFORE any other click handling (tunneling) — closes an actively-edited
        // note or board title if the click landed outside it

        private static bool IsPartOfControl(DependencyObject? element, DependencyObject ancestor)
        {
            while (element != null)
            {
                if (element == ancestor) return true;

                if (element is FrameworkElement fe && fe.TemplatedParent != null)
                {
                    element = fe.TemplatedParent;
                    continue;
                }

                element = LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        private void RootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickTarget = e.OriginalSource as DependencyObject;

            // Clicks inside the Sidebar (Note/Board buttons, Color, the format
            // toolbar) should NEVER exit note-editing mode — these tools are
            // meant to be used WHILE actively editing a note. Without this
            // check, clicking e.g. Bold registered as "clicked outside the
            // note" and exited edit mode before the format could even apply,
            // causing the toggle-then-reset loop.
            bool clickInsideSidebar = IsDescendantOf(clickTarget, Sidebar);

            // Text Style's popup renders in a SEPARATE top-level OS window
            // (that's how Popup works), so IsDescendantOf can never reach it
            // via the normal visual tree — checked separately here using the
            // same cross-boundary helper built earlier for this exact popup.
            bool clickInsideTextStylePopover =
                activeTextStylePicker != null && IsPartOfControl(clickTarget, activeTextStylePicker);

            if (clickInsideSidebar || clickInsideTextStylePopover) return;

            if (Keyboard.FocusedElement is RichTextBox noteBox && noteBox.DataContext is NoteItemViewModel note && note.IsEditing)
            {
                if (!IsDescendantOf(clickTarget, noteBox))
                {
                    note.IsEditing = false;
                    Keyboard.Focus(RootGrid);
                }
            }
            else if (Keyboard.FocusedElement is TextBox editBox)
            {
                if (editBox.DataContext is BoardViewModel board && board.IsEditingTitle)
                {
                    if (!IsDescendantOf(clickTarget, editBox))
                    {
                        board.CommitTitle();
                        Keyboard.Focus(RootGrid);
                    }
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
            System.Diagnostics.Debug.WriteLine("Sidebar_MouseEnter");
            IsSidebarExpanded = true;
            AnimateSidebarWidth(180);
        }

        private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isColorPopoverOpen) // Text Style no longer participates in this check
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
