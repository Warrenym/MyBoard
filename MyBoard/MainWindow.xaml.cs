using MyBoard.Services;
using MyBoard.ViewModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyBoard
{
   
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModel.MainViewModel();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown_Pan;
            PreviewKeyUp += MainWindow_PreviewKeyUp_Pan;
            ((ViewModel.MainViewModel)DataContext).BoardColorPickerRequested += (s, hex) =>
                BoardColorPicker.LoadColor(hex);
            Closing += MainWindow_Closing;

            var viewModel = (ViewModel.MainViewModel)DataContext;
            viewModel.CurrentBoard.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.BoardViewModel.PrimarySelectedItem))
                    SlideSidebarPanel(viewModel.CurrentBoard.PrimarySelectedItem != null);
            };
        }

        private bool isSelecting;
        private bool didDrag;
        private Point selectionStartPoint;


        // Called continuously while something is dragged over the canvas — decides whether to show the "can drop here" cursor
        private void BoardCanvas_DragEnter(object sender, DragEventArgs e)
        {
            bool canAccept = e.Data.GetDataPresent(DataFormats.FileDrop)
                            || e.Data.GetDataPresent(DataFormats.Bitmap)
                            || e.Data.GetDataPresent(DataFormats.Text)
                            || e.Data.GetDataPresent(DataFormats.Html);

            e.Effects = canAccept ? DragDropEffects.Copy : DragDropEffects.None;
        }


        // Fires when the item is actually dropped — detects the data type and routes to the right handling case
        private async void BoardCanvas_Drop(object sender, DragEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;
                             Point rawPosition = e.GetPosition(CanvasViewport);

            Point dropPosition = new((rawPosition.X - viewModel.PanX) / viewModel.ZoomLevel,
                                     (rawPosition.Y - viewModel.PanY) / viewModel.ZoomLevel);


            // Case 1: File Explorer — real file paths
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var path in files.Where(IsImageFile))
                {
                    string saved = ImageStorageService.CopyFile(path);
                    viewModel.CurrentBoard.AddImage(saved, dropPosition.X, dropPosition.Y);
                }
                return;
            }


            // Case 2: Raw bitmap data — some apps supply pixels directly
            if (e.Data.GetDataPresent(DataFormats.Bitmap) &&
                e.Data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            {
                string saved = ImageStorageService.SaveBitmap(bitmap);
                viewModel.CurrentBoard.AddImage(saved, dropPosition.X, dropPosition.Y);
                return;
            }


            // Case 3: Browser images — extract a URL, then download it
            string? url = ExtractImageUrl(e.Data);
            if (url != null)
            {
                try
                {
                    string saved = await ImageStorageService.DownloadImageAsync(url);
                    viewModel.CurrentBoard.AddImage(saved, dropPosition.X, dropPosition.Y);
                }
                catch
                {
                    MessageBox.Show("Couldn't download that image — try saving it locally first.");
                }
            }
        }

        private const double SidebarPanelWidth = 180;
        // Slides the sidebar's inner panel to reveal Design tools when something is selected
        private void SlideSidebarPanel(bool showDesignPanel)
        {
            

            var animation = new DoubleAnimation
            {
                To = showDesignPanel ? -SidebarPanelWidth : 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                FillBehavior = FillBehavior.HoldEnd
            };

            SidebarSlideTransform.BeginAnimation(
                TranslateTransform.XProperty,
                animation);
        }

        private void ColorPopup_Opened(object sender, EventArgs e)
        {
            var popup = (Popup)sender;
            Point screenPoint = ColorToolButton.PointToScreen(
                new Point(ColorToolButton.ActualWidth + 8, 0));

            popup.HorizontalOffset = screenPoint.X;
            popup.VerticalOffset = screenPoint.Y;
        }

        private void Board_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Board_PropertyChanged fired: {e.PropertyName}");

            if (e.PropertyName == nameof(ViewModel.BoardViewModel.PrimarySelectedItem) && sender is ViewModel.BoardViewModel board)
            {
                System.Diagnostics.Debug.WriteLine($"PrimarySelectedItem changed, value is null? {board.PrimarySelectedItem == null}");
                SlideSidebarPanel(board.PrimarySelectedItem != null);
            }
        }

        // Auto-focuses the title TextBox the moment it appears, and selects all text so typing immediately replaces it (matches the Note editing pattern)
        private void BoardTitleEditBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not ViewModel.BoardViewModel board) return;
            if (!board.IsEditingTitle) return; // Don't steal focus if this fires for an already-committed board

            // Still deferred one more tick via Dispatcher — Loaded confirms the element
            // EXISTS in the tree, but layout (size/position) may finish a moment later
            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox); // Explicitly moves keyboard input focus, not just logical focus
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }


        // Handles focus for RE-entering edit mode (e.g. double-click rename)
        private void BoardTitleEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.Visibility != Visibility.Visible) return;
            if (textBox.DataContext is not ViewModel.BoardViewModel board || !board.IsEditingTitle) return;

            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }


        // Enter or Escape both commit the title (Escape doesn't cancel/revert here, since an empty board name isn't a meaningful "undo" state to return to)
        private void BoardTitleEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not ViewModel.BoardViewModel board)
                return;

            if (e.Key == Key.Enter)
            {
                board.CommitTitle();

                // Enter also exits the selected/highlighted state entirely, since the
                // user is explicitly signaling "I'm done" rather than just clicking away
                if (Window.GetWindow(textBox)?.DataContext is ViewModel.MainViewModel mainViewModel)
                    mainViewModel.CurrentBoard.ClearSelection();

                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                board.CommitTitle();
                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
        }


        // Double-clicking the title text renames the board; single-clicking it still selects/drags normally by letting the click bubble up unhandled
        private void BoardTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ViewModel.BoardViewModel board)
                return;

            if (e.ClickCount == 2)
            {
                if (Window.GetWindow(element)?.DataContext is ViewModel.MainViewModel mainViewModel)
                    mainViewModel.CurrentBoard.SelectItem(board);

                board.BeginEditingTitle(); 
                e.Handled = true;
            }
        }


        // Fires every time the color picker's color changes — 
        // Applies it instantly to whichever board is currently selected, satisfying the "updates nstantly" sync requirement
        private void BoardColorPicker_ColorSelected(object? sender, string hexColor)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;
            if (viewModel.CurrentBoard.PrimarySelectedItem is ViewModel.BoardViewModel board)
                board.Color = hexColor;
        }


        private static bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }


        // Browsers vary in what they hand over — checks plain-text URLs first, then falls back to parsing an <img> tag out of an HTML fragment
        private static string? ExtractImageUrl(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.Text))
            {
                string text = ((string)data.GetData(DataFormats.Text)).Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                    return text;
            }

            if (data.GetDataPresent(DataFormats.Html))
            {
                string html = (string)data.GetData(DataFormats.Html);
                var match = Regex.Match(html, "<img[^>]+src=[\"']([^\"']+)[\"']");
                if (match.Success)
                    return match.Groups[1].Value;
            }

            return null;
        }


        // Zooms in/out anchored at the mouse cursor:
        // The canvas point currently under the mouse stays under the mouse after the zoom level changes, by solving for the pan offset that keeps that point fixed on screen.
        private void BoardCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;

            Point mousePos = e.GetPosition(CanvasViewport);
            double oldZoom = viewModel.ZoomLevel;

            double change = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = Math.Clamp(oldZoom + change, 0.4, 3.0);

            if (newZoom == oldZoom)
            {
                e.Handled = true;
                return; // Already at min/max zoom — nothing to adjust
            }

            // Find which canvas-space point is currently under the cursor,
            // using the CURRENT zoom/pan before anything changes
            double canvasX = (mousePos.X - viewModel.PanX) / oldZoom;
            double canvasY = (mousePos.Y - viewModel.PanY) / oldZoom;

            viewModel.ZoomLevel = newZoom;

            // Recalculate pan so that same canvas point lands back under the cursor
            // at the NEW zoom level
            viewModel.PanX = mousePos.X - (canvasX * newZoom);
            viewModel.PanY = mousePos.Y - (canvasY * newZoom);

            e.Handled = true;
        }


        // Shared handler for both Note and Image resize thumbs.
        // Thumb.DragDelta already reports values in the item's local coordinate space, automatically accounting for the canvas's zoom transform — no manual scaling needed
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb) return;
            if (thumb.DataContext is not IResizable resizable) return;

            resizable.Width = Math.Max(60, resizable.Width + e.HorizontalChange);
            resizable.Height = Math.Max(40, resizable.Height + e.VerticalChange);
        }

        // Auto-focuses the TextBox the moment it becomes visible (edit mode starts), and selects all text so typing immediately replaces the placeholder content
        private void NoteEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Visibility == Visibility.Visible)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }


        // Exits edit mode when the user clicks away — focus naturally leaves the TextBox the moment they click anywhere else (another item, empty canvas, etc.)
        private void NoteEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is ViewModel.NoteItemViewModel note)
                note.IsEditing = false;
        }


        // Escape exits edit mode without requiring a click elsewhere
        private void NoteEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape &&
                sender is TextBox textBox &&
                textBox.DataContext is ViewModel.NoteItemViewModel note)
            {
                note.IsEditing = false;
                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
        }


        // Runs BEFORE any other click handling in the window (tunneling), so it can close an actively-edited note before that same click is processed for selection/dragging elsewhere. Fixes LostFocus not firing reliably.
        private void RootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is not TextBox editBox) return;

            if (editBox.DataContext is ViewModel.NoteItemViewModel note && note.IsEditing)
            {
                if (!IsDescendantOf(e.OriginalSource as DependencyObject, editBox))
                {
                    note.IsEditing = false;
                    Keyboard.Focus(RootGrid);
                }
            }
            else if (editBox.DataContext is ViewModel.BoardViewModel board && board.IsEditingTitle)
            {
                if (!IsDescendantOf(e.OriginalSource as DependencyObject, editBox))
                {
                    board.CommitTitle();
                    Keyboard.Focus(RootGrid);
                }
            }
        }

        // Safety net: after ANY click anywhere in the window, clear keyboard focus from whatever was clicked (if it's not a text-input control).
        // Prevents buttons from retaining focus and hijacking the Space key for their own
        private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is Button)
                Keyboard.Focus(RootGrid); 
        }


        // Walks up the visual tree checking if 'child' is inside 'ancestor'
        private static bool IsDescendantOf(DependencyObject? child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return false;
        }      


        // Tracks whether Space is currently held — while true, LMB-drag pans the canvas instead of selecting/dragging items underneath the cursor
        private bool isSpaceHeld;
        private bool isPanning;
        private Point lastPanMousePosition;


        // Deletes the selected item on Delete/Backspace — but only when the user isn't actively typing in a TextBox, so editing note text still works normally
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isTyping = Keyboard.FocusedElement is TextBox;
            var board = ((ViewModel.MainViewModel)DataContext).CurrentBoard;

            if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
            {
                ((ViewModel.MainViewModel)DataContext).CurrentBoard.DeleteSelectedItems();
            }

            // Ctrl+S triggers a manual save
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ((ViewModel.MainViewModel)DataContext).SaveBoardCommand.Execute(null);
            }

            if (e.Key == Key.Delete && !isTyping)
                board.DeleteSelectedItems();

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                ((ViewModel.MainViewModel)DataContext).SaveBoardCommand.Execute(null);

            // Standard cut/copy/paste/duplicate shortcuts — skipped while typing so normal text editing (Ctrl+C inside a note, etc.) isn't hijacked
            if (!isTyping && Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z: ((ViewModel.MainViewModel)DataContext).UndoCommand.Execute(null); break;
                    case Key.Y: ((ViewModel.MainViewModel)DataContext).RedoCommand.Execute(null); break;
                    case Key.C: board.CopySelectedItems(); break;
                    case Key.X: board.CutSelectedItems(); break;
                    case Key.V: board.PasteClipboard(lastCanvasMousePosition.X, lastCanvasMousePosition.Y); break;
                    case Key.D: board.DuplicateSelectedItems(); break;
                }
            }

            // F2 renames a single selected board — same convention as Windows Explorer
            if (e.Key == Key.F2 && !isTyping &&
                board.SelectedItems.Count == 1 &&
                board.SelectedItems[0] is ViewModel.BoardViewModel boardToRename)
            {
                boardToRename.BeginEditingTitle(); // was boardViewModel
            }

            if (e.Key == Key.Escape)
                ((ViewModel.MainViewModel)DataContext).IsColorPopoverOpen = false;
        }


        // Safety net — saves automatically on exit in case the user forgets to Ctrl+S
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((ViewModel.MainViewModel)DataContext).SaveBoardCommand.Execute(null);
        }


        // Space key down: enable pan mode, switch cursor to indicate it
        private void MainWindow_PreviewKeyDown_Pan(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !isSpaceHeld)
            {
                isSpaceHeld = true;
                Mouse.OverrideCursor = Cursors.Hand; // Forces the cursor everywhere, not just on CanvasViewport
            }
        }


        private void MainWindow_PreviewKeyUp_Pan(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                isSpaceHeld = false;
                isPanning = false;
                Mouse.OverrideCursor = null; // Reverts to whatever each element's own Cursor normally is
            }
        }


        // Safety net: if the window loses focus while Space is held (e.g. Alt-Tab), there's no KeyUp to catch — force pan mode off so the cursor doesn't get stuck
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            isSpaceHeld = false;
            isPanning = false;
            isSelecting = false;
            Mouse.OverrideCursor = null;

            if (CanvasViewport.IsMouseCaptured)
                CanvasViewport.ReleaseMouseCapture();
        }


        // Tracks the mouse's position in true canvas coordinate
        private Point lastCanvasMousePosition;
        private void CanvasViewport_MouseMove(object sender, MouseEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;

            Point rawPos = e.GetPosition(CanvasViewport);
            lastCanvasMousePosition = new Point(
                (rawPos.X - viewModel.PanX) / viewModel.ZoomLevel,
                (rawPos.Y - viewModel.PanY) / viewModel.ZoomLevel);

            if (isPanning)
            {
                Point currentPos = e.GetPosition(CanvasViewport);
                viewModel.PanX += currentPos.X - lastPanMousePosition.X;
                viewModel.PanY += currentPos.Y - lastPanMousePosition.Y;
                lastPanMousePosition = currentPos;
                return;
            }

            if (isSelecting)
            {
                Point currentPos = e.GetPosition(CanvasViewport);
                didDrag = true; // Any movement at all counts as a real drag, not just a click

                double left = Math.Min(selectionStartPoint.X, currentPos.X);
                double top = Math.Min(selectionStartPoint.Y, currentPos.Y);

                SelectionBox.Margin = new Thickness(left, top, 0, 0);
                SelectionBox.Width = Math.Abs(currentPos.X - selectionStartPoint.X);
                SelectionBox.Height = Math.Abs(currentPos.Y - selectionStartPoint.Y);
                SelectionBox.Visibility = Visibility.Visible;
            }
        }


        // Fires BEFORE any item's own click handling (tunneling) — if Space is held, this starts a pan and blocks the click from reaching items underneath
        private void CanvasViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isSpaceHeld) return;

            isPanning = true;
            lastPanMousePosition = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
            Mouse.OverrideCursor = Cursors.SizeAll; // Actively panning — replaces the "ready to pan" hand cursor
            e.Handled = true;
        }


        // Starts either a pan (if Space is held — handled in the Preview handler below), 
        // Or a selection-box drag. Only reaches here for clicks on truly empty canvas space, since clicks on items already mark the event Handled before bubbling this far.
        private void CanvasViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isSpaceHeld) return; // Panning is handled separately via the Preview handler

            isSelecting = true;
            didDrag = false;
            selectionStartPoint = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
        }


        private void CanvasViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;

            if (isPanning)
            {
                isPanning = false;
                CanvasViewport.ReleaseMouseCapture();
                Mouse.OverrideCursor = isSpaceHeld ? Cursors.Hand : null;
                return;
            }

            if (isSelecting)
            {
                isSelecting = false;
                CanvasViewport.ReleaseMouseCapture();
                SelectionBox.Visibility = Visibility.Collapsed;

                if (didDrag)
                    SelectItemsInBox(viewModel); // A real drag happened — select whatever's inside the box
                else
                    viewModel.CurrentBoard.ClearSelection(); // No movement — this was just a click on empty space
            }
        }


        // Finds every item whose rendered bounds intersect the selection rectangle.
        // Uses TransformToAncestor to convert each item's bounds into the same screen-space coordinates as the selection box — this automatically accounts for the canvas's current zoom and pan without needing to manually invert that math.
        private void SelectItemsInBox(ViewModel.MainViewModel viewModel)
        {
            Rect selectionRect = new(
                SelectionBox.Margin.Left, SelectionBox.Margin.Top,
                SelectionBox.Width, SelectionBox.Height);

            var selected = new List<object>();

            for (int i = 0; i < BoardCanvas.Items.Count; i++)
            {
                if (BoardCanvas.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                    continue;

                Rect itemBounds = new(0, 0, container.ActualWidth, container.ActualHeight);
                Rect itemBoundsInViewport = container.TransformToAncestor(CanvasViewport)
                                                      .TransformBounds(itemBounds);

                if (selectionRect.IntersectsWith(itemBoundsInViewport))
                    selected.Add(BoardCanvas.Items[i]);
            }

            viewModel.CurrentBoard.SelectItems(selected);
        }


        //Click menu hanlders
        private void MenuItem_Cut_Click(object sender, RoutedEventArgs e) =>
            ((ViewModel.MainViewModel)DataContext).CurrentBoard.CutSelectedItems();

        private void MenuItem_Copy_Click(object sender, RoutedEventArgs e) =>
            ((ViewModel.MainViewModel)DataContext).CurrentBoard.CopySelectedItems();

        private void MenuItem_Duplicate_Click(object sender, RoutedEventArgs e) =>
            ((ViewModel.MainViewModel)DataContext).CurrentBoard.DuplicateSelectedItems();

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e) =>
            ((ViewModel.MainViewModel)DataContext).CurrentBoard.DeleteSelectedItems();

        private void MenuItem_Rename_Click(object sender, RoutedEventArgs e)
        {
            var board = ((ViewModel.MainViewModel)DataContext).CurrentBoard;
            if (board.SelectedItems.Count == 1 && board.SelectedItems[0] is ViewModel.BoardViewModel boardToRename)
                boardToRename.BeginEditingTitle(); // was boardViewModel
        }

        private void MenuItem_PasteCanvas_Click(object sender, RoutedEventArgs e)
        {
            var board = ((ViewModel.MainViewModel)DataContext).CurrentBoard;
            board.PasteClipboard(lastCanvasMousePosition.X, lastCanvasMousePosition.Y);
        }
    }
}