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
        // Tracks the mouse position from the last MouseMove, so we can calculate delta movement
        private static Point lastMousePosition;

        // Tracks whether a drag is currently in progress
        private static bool isDragging;

        // The attached property: set IsDraggable="True" in XAML to enable dragging on an element
        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.RegisterAttached(
                "IsDraggable",
                typeof(bool),
                typeof(DraggableBehavior),
                new PropertyMetadata(false, OnIsDraggableChanged));

        public static void SetIsDraggable(UIElement element, bool value) =>
            element.SetValue(IsDraggableProperty, value);

        public static bool GetIsDraggable(UIElement element) =>
            (bool)element.GetValue(IsDraggableProperty);

        // Runs once when IsDraggable is set — hooks up the mouse event handlers
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

        // Tracks the last click's time and target, to distinguish a real double-click
        // (two clicks in quick succession) from two separate, deliberate single clicks.
        private static DateTime lastClickTime = DateTime.MinValue;
        private static object? lastClickedItem;

        // Handles both selection and board-opening:
        // - Any click selects the item (or re-selects it, if already selected).
        // - A SECOND click within the OS's double-click time window on the SAME item opens it.
        // - A click after a pause — even on the same item — is treated as a fresh single click.
        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var clickedItem = element.DataContext;

            DateTime now = DateTime.Now;
            double millisecondsSinceLastClick = (now - lastClickTime).TotalMilliseconds;

            [DllImport("user32.dll")]
            static extern int GetDoubleClickTime();

            // SystemParameters.DoubleClickTime respects the user's actual OS double-click speed setting
            bool isRealDoubleClick = ReferenceEquals(clickedItem, lastClickedItem)
                                     && millisecondsSinceLastClick <= GetDoubleClickTime();

            lastClickTime = now;
            lastClickedItem = clickedItem;

            if (Window.GetWindow(element)?.DataContext is ViewModel.MainViewModel mainViewModel)
            {
                if (isRealDoubleClick && clickedItem is ViewModel.BoardViewModel boardToOpen)
                {
                    mainViewModel.NavigateToBoardCommand.Execute(boardToOpen);
                    lastClickTime = DateTime.MinValue; // Reset so opening doesn't chain into another open
                    e.Handled = true;
                    return;
                }

                mainViewModel.CurrentBoard.SelectItem(clickedItem);
            }

            isDragging = true;
            lastMousePosition = e.GetPosition(GetCanvasParent(element));
            element.CaptureMouse();
            e.Handled = true;
        }

        // While dragging, calculates how far the mouse moved since the last event and applies that delta directly to the bound ViewModel's X/Y
        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not IPositionable positionable) return;

            var canvas = GetCanvasParent(element);
            var currentPosition = e.GetPosition(canvas);

            double deltaX = currentPosition.X - lastMousePosition.X;
            double deltaY = currentPosition.Y - lastMousePosition.Y;

            positionable.X += deltaX;
            positionable.Y += deltaY;

            lastMousePosition = currentPosition;
        }

        // Ends the drag and releases mouse capture
        private static void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            ((FrameworkElement)sender).ReleaseMouseCapture();
        }

        // Walks up the visual tree to find the parent Canvas —
        // needed so mouse positions are measured relative to the canvas, not the item itself
        private static UIElement GetCanvasParent(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null && current is not System.Windows.Controls.Canvas)
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);

            return current as UIElement ?? element;
        }
    }
}
