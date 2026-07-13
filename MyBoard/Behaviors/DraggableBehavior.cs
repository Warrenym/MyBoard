using MyBoard.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
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
            }
            else
            {
                element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                element.MouseMove -= Element_MouseMove;
                element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
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

            if (Window.GetWindow(element)?.DataContext is MainViewModel mainViewModel)
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

                // If this item is already part of a multi-selection, keep the whole selection intact instead of collapsing to just this one — that's what allows the drag below to move every selected item together.
                bool alreadyInSelection = board.SelectedItems.Contains(clickedItem);
                if (!alreadyInSelection)
                    board.SelectItem(clickedItem);
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
            ((FrameworkElement)sender).ReleaseMouseCapture();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetDoubleClickTime();

        private static UIElement GetCanvasParent(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null && current is not System.Windows.Controls.Canvas)
                   current = System.Windows.Media.VisualTreeHelper.GetParent(current);

            return current as UIElement ?? element;
        }
    }
}
