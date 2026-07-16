using MyBoard.Services;
using MyBoard.ViewModel;
using MyBoard.Commands;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MyBoard
{
    public partial class MainWindow
    {
        private bool isSelecting;
        private bool didDrag;
        private Point selectionStartPoint;
        private bool isPanning;
        private Point lastPanMousePosition;
        private Point lastCanvasMousePosition;

        private void BoardCanvas_DragEnter(object sender, DragEventArgs e)
        {
            bool canAccept = e.Data.GetDataPresent(DataFormats.FileDrop)
                            || e.Data.GetDataPresent(DataFormats.Bitmap)
                            || e.Data.GetDataPresent(DataFormats.Text)
                            || e.Data.GetDataPresent(DataFormats.Html);

            e.Effects = canAccept ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void BoardCanvas_Drop(object sender, DragEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            Point dropPosition = CanvasCoordinateService.ScreenToCanvas(
                e.GetPosition(CanvasViewport), viewModel.PanX, viewModel.PanY, viewModel.ZoomLevel);

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

            if (e.Data.GetDataPresent(DataFormats.Bitmap) &&
                e.Data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            {
                string saved = ImageStorageService.SaveBitmap(bitmap);
                viewModel.CurrentBoard.AddImage(saved, dropPosition.X, dropPosition.Y);
                return;
            }

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

        private static bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

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

        private void BoardCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            Point mousePos = e.GetPosition(CanvasViewport);
            double oldZoom = viewModel.ZoomLevel;

            double change = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = Math.Clamp(oldZoom + change, 0.4, 3.0);

            if (newZoom == oldZoom) { e.Handled = true; return; }

            Point canvasPoint = CanvasCoordinateService.ScreenToCanvas(mousePos, viewModel.PanX, viewModel.PanY, oldZoom);
            viewModel.ZoomLevel = newZoom;
            viewModel.PanX = mousePos.X - (canvasPoint.X * newZoom);
            viewModel.PanY = mousePos.Y - (canvasPoint.Y * newZoom);

            e.Handled = true;
        }


        private double resizeStartWidth;
        private double resizeStartHeight;
        private IResizable? resizingItem;
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb) return;
            if (thumb.DataContext is not IResizable resizable) return;

            // First delta of a new resize — capture the starting size once
            if (resizingItem != resizable)
            {
                resizingItem = resizable;
                resizeStartWidth = resizable.Width;
                resizeStartHeight = resizable.Height;
            }

            // Images resize proportionally
            if (thumb.DataContext is ImageItemViewModel image && image.AspectRatio > 0)
            {
                double newWidth = Math.Max(60, image.Width + e.HorizontalChange);
                image.Width = newWidth;
                image.Height = newWidth / image.AspectRatio;
                return;
            }

            resizable.Width = Math.Max(60, resizable.Width + e.HorizontalChange);
            resizable.Height = Math.Max(40, resizable.Height + e.VerticalChange);
        }


        // Records the completed resize as a single undo step, once the drag ends
        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (resizingItem == null) return;
            if ((MainViewModel)DataContext is not MainViewModel viewModel) return;

            if (resizingItem.Width != resizeStartWidth || resizingItem.Height != resizeStartHeight)
            {
                viewModel.UndoRedo.Record(new ResizeItemCommand(
                    resizingItem, resizeStartWidth, resizeStartHeight, resizingItem.Width, resizingItem.Height));
            }

            resizingItem = null;
        }


        private void MainWindow_PreviewKeyDown_Pan(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !isSpaceHeld)
            {
                isSpaceHeld = true;
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }


        private void MainWindow_PreviewKeyUp_Pan(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                isSpaceHeld = false;
                isPanning = false;
                Mouse.OverrideCursor = null;
            }
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            isSpaceHeld = false;
            isPanning = false;
            isSelecting = false;
            Mouse.OverrideCursor = null;

            if (CanvasViewport.IsMouseCaptured)
                CanvasViewport.ReleaseMouseCapture();
        }

        private void CanvasViewport_MouseMove(object sender, MouseEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            Point rawPos = e.GetPosition(CanvasViewport);
            lastCanvasMousePosition = CanvasCoordinateService.ScreenToCanvas(rawPos, viewModel.PanX, viewModel.PanY, viewModel.ZoomLevel);

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
                didDrag = true;

                double left = Math.Min(selectionStartPoint.X, currentPos.X);
                double top = Math.Min(selectionStartPoint.Y, currentPos.Y);

                SelectionBox.Margin = new Thickness(left, top, 0, 0);
                SelectionBox.Width = Math.Abs(currentPos.X - selectionStartPoint.X);
                SelectionBox.Height = Math.Abs(currentPos.Y - selectionStartPoint.Y);
                SelectionBox.Visibility = Visibility.Visible;
            }
        }

        private void CanvasViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isSpaceHeld) return;

            isPanning = true;
            lastPanMousePosition = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
            Mouse.OverrideCursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void CanvasViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isSpaceHeld) return;

            isSelecting = true;
            didDrag = false;
            selectionStartPoint = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
        }

        private void CanvasViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;

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
                    SelectItemsInBox(viewModel);
                else
                    viewModel.CurrentBoard.ClearSelection();
            }
        }

        private void SelectItemsInBox(MainViewModel viewModel)
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
                Rect itemBoundsInViewport = container.TransformToAncestor(CanvasViewport).TransformBounds(itemBounds);

                if (selectionRect.IntersectsWith(itemBoundsInViewport))
                    selected.Add(BoardCanvas.Items[i]);
            }

            viewModel.CurrentBoard.SelectItems(selected);
        }
    }
}