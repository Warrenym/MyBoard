using MyBoard.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MyBoard.Behaviors
{
    internal class DraggableBehavior
    {
        private static Point lastMousePosition;
        private static bool isDragging;
        private static DateTime lastClickTime = DateTime.MinValue;
        private static object? lastClickedItem;

        // Cached so MouseMove doesn't need to re-walk the visual tree on every event
        private static MainViewModel? activeMainViewModel;

        public static readonly DependencyProperty IsDraggableProperty =
                DependencyProperty.RegisterAttached(
                "IsDraggable", typeof(bool), typeof(DraggableBehavior),
                new PropertyMetadata(false, OnIsDraggableChanged));


        // Captures each selected item's position at the START of a drag
        private static List<(IPositionable item, double startX, double startY)> dragStartPositions = new();
        public static void SetIsDraggable(UIElement element, bool value) => element.SetValue(IsDraggableProperty, value);
        public static bool GetIsDraggable(UIElement element) =>(bool)element.GetValue(IsDraggableProperty);


        private static void OnIsDraggableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;

            if ((bool)e.NewValue)
            {
                element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                element.MouseMove += Element_MouseMove;
                element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                element.PreviewMouseRightButtonDown += Element_PreviewMouseRightButtonDown; // new
            }
            else
            {
                element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                element.MouseMove -= Element_MouseMove;
                element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
                element.PreviewMouseRightButtonDown -= Element_PreviewMouseRightButtonDown; // new
            }
        }


        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var clickedItem = element.DataContext;

            if (clickedItem is NoteItemViewModel editingNote && editingNote.IsEditing)
                return;

            if (clickedItem is BoardViewModel editingBoard && editingBoard.IsEditingTitle)
                return;

            DateTime now = DateTime.Now;
            double millisecondsSinceLastClick = (now - lastClickTime).TotalMilliseconds;
            bool isRealDoubleClick = ReferenceEquals(clickedItem, lastClickedItem)
                                     && millisecondsSinceLastClick <= GetDoubleClickTime();

            lastClickTime = now;
            lastClickedItem = clickedItem;



            var mainViewModel = Window.GetWindow(element)?.DataContext as MainViewModel;

            if (mainViewModel != null)
            {
                activeMainViewModel = mainViewModel;
                var board = mainViewModel.CurrentBoard;

                if (isRealDoubleClick && clickedItem is BoardViewModel boardToOpen)
                {
                    mainViewModel.NavigateToBoardCommand.Execute(boardToOpen);
                    lastClickTime = DateTime.MinValue;
                    e.Handled = true;
                    return;
                }

                if (isRealDoubleClick && clickedItem is NoteItemViewModel noteToEdit)
                {
                    board.SelectItem(clickedItem);
                    noteToEdit.IsEditing = true;
                    lastClickTime = DateTime.MinValue;
                    e.Handled = true;
                    return;
                }

                bool alreadyInSelection = board.SelectedItems.Contains(clickedItem);
                if (!alreadyInSelection)
                    board.SelectItem(clickedItem);
            }

            // Capture starting positions for undo, now that mainViewModel is accessible here too (previously out of scope at this point)
            dragStartPositions.Clear();
            if (mainViewModel != null)
            {
                var itemsBeingDragged = (mainViewModel.CurrentBoard.SelectedItems.Count > 1 &&
                                          mainViewModel.CurrentBoard.SelectedItems.Contains(clickedItem))
                    ? mainViewModel.CurrentBoard.SelectedItems
                    : new ObservableCollection<object> { clickedItem };

                foreach (var item in itemsBeingDragged)
                    if (item is IPositionable positionable)
                        dragStartPositions.Add((positionable, positionable.X, positionable.Y));
            }

            isDragging = true;
            lastMousePosition = e.GetPosition(GetCanvasParent(element));
            element.CaptureMouse();
            e.Handled = true;
        }


        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            if (sender is not FrameworkElement element) return;

            var canvas = GetCanvasParent(element);
            var currentPosition = e.GetPosition(canvas);

            double deltaX = currentPosition.X - lastMousePosition.X;
            double deltaY = currentPosition.Y - lastMousePosition.Y;

            // If multiple items are selected, move all of them together by the same delta. Otherwise, fall back to moving just the single item under the cursor.
            var selectedItems = activeMainViewModel?.CurrentBoard.SelectedItems;

            if (selectedItems != null && selectedItems.Count > 1 &&
                selectedItems.Contains(element.DataContext))
            {
                foreach (var item in selectedItems)
                {
                    if (item is IPositionable positionable)
                    {
                        positionable.X += deltaX;
                        positionable.Y += deltaY;
                    }
                }
            }
            else if (element.DataContext is IPositionable single)
            {
                single.X += deltaX;
                single.Y += deltaY;
            }

            lastMousePosition = currentPosition;
        }


        private static void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            var element = (FrameworkElement)sender;
            element.ReleaseMouseCapture();

            if (dragStartPositions.Count > 0 &&
                Window.GetWindow(element)?.DataContext is ViewModel.MainViewModel mainViewModel)
            {
                var moves = dragStartPositions
                    .Where(d => d.item.X != d.startX || d.item.Y != d.startY) // skip if it never actually moved
                    .Select(d => (d.item, d.startX, d.startY, d.item.X, d.item.Y))
                    .ToList();

                if (moves.Count > 0)
                    mainViewModel.UndoRedo.Record(new Commands.MoveItemsCommand(moves));
            }

            dragStartPositions.Clear();
        }

        //Double clicking
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetDoubleClickTime();

        private static UIElement GetCanvasParent(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null && current is not System.Windows.Controls.Canvas)
                   current = System.Windows.Media.VisualTreeHelper.GetParent(current);

            return current as UIElement ?? element;
        }


        // Right-clicking selects the item BEFORE the context menu opens
        private static void Element_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var clickedItem = element.DataContext;

            if (Window.GetWindow(element)?.DataContext is ViewModel.MainViewModel mainViewModel)
            {
                var board = mainViewModel.CurrentBoard;
                if (!board.SelectedItems.Contains(clickedItem))
                    board.SelectItem(clickedItem);
            }
        }
    }

}
