using MyBoard.Services;
using MyBoard.ViewModel;
using MyBoard.Commands;
using System.Net;
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

            await ImportImagesAsync(e.Data, dropPosition);
            e.Handled = true;
        }

        private async Task<bool> ImportImagesAsync(IDataObject data, Point canvasPosition)
        {
            var board = ((MainViewModel)DataContext).CurrentBoard;
            int importedCount = 0;

            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var path in files.Where(IsImageFile))
                {
                    string saved = ImageStorageService.CopyFile(path, ((MainViewModel)DataContext).ImageFolder);
                    board.AddImage(saved, canvasPosition.X + (importedCount * 20), canvasPosition.Y + (importedCount * 20));
                    importedCount++;
                }

                if (importedCount > 0) return true;
            }

            if (data.GetDataPresent(DataFormats.Bitmap) &&
                data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            {
                string saved = ImageStorageService.SaveBitmap(bitmap, ((MainViewModel)DataContext).ImageFolder);
                board.AddImage(saved, canvasPosition.X, canvasPosition.Y);
                return true;
            }

            string? url = ExtractImageUrl(data);
            if (url != null)
            {
                try
                {
                    string saved = url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                        ? ImageStorageService.SaveDataUri(url, ((MainViewModel)DataContext).ImageFolder)
                        : await ImageStorageService.DownloadImageAsync(
                            url, ((MainViewModel)DataContext).ImageFolder);
                    board.AddImage(saved, canvasPosition.X, canvasPosition.Y);
                    return true;
                }
                catch
                {
                    MessageBox.Show("Couldn't paste that image — try saving it locally first.");
                }
            }

            return false;
        }

        private async Task PasteAtCanvasPositionAsync(Point canvasPosition)
        {
            IDataObject? data;
            try
            {
                data = Clipboard.GetDataObject();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show("The clipboard is busy. Please try pasting again.");
                return;
            }

            var board = ((MainViewModel)DataContext).CurrentBoard;
            if (data != null && ClipboardService.OwnsSystemClipboard(data))
            {
                board.PasteClipboard(canvasPosition.X, canvasPosition.Y);
                return;
            }

            if (data != null && await ImportImagesAsync(data, canvasPosition))
                return;

            // Retain in-app paste if setting the Windows clipboard marker failed.
            board.PasteClipboard(canvasPosition.X, canvasPosition.Y);
        }

        private static bool IsImageFile(string path)
        {
            return ImageStorageService.IsSupportedImage(path);
        }

        private static string? ExtractImageUrl(IDataObject data)
        {
            // Browser clipboards often include both the page URL as plain text and
            // the actual image URL in HTML, so prefer the image-specific HTML.
            if (data.GetDataPresent(DataFormats.Html))
            {
                string html = (string)data.GetData(DataFormats.Html);
                var match = Regex.Match(html, "<img[^>]+src\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (match.Success)
                    return WebUtility.HtmlDecode(match.Groups[1].Value);
            }

            if (data.GetDataPresent(DataFormats.Text))
            {
                string text = ((string)data.GetData(DataFormats.Text)).Trim();
                if (text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    return text;

                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                    return text;
            }

            return null;
        }

        private void BoardCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            Point mousePos = e.GetPosition(CanvasViewport);
            double oldZoom = viewModel.ZoomLevel;

            double change = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = CanvasCoordinateService.ClampZoom(oldZoom + change);

            if (newZoom == oldZoom) { e.Handled = true; return; }

            Point pan = CanvasCoordinateService.PanForZoom(mousePos, viewModel.PanX, viewModel.PanY, oldZoom, newZoom);
            viewModel.ZoomLevel = newZoom;
            viewModel.PanX = pan.X;
            viewModel.PanY = pan.Y;

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

            var size = CanvasCoordinateService.Resize(resizable.Width, resizable.Height,
                e.HorizontalChange, e.VerticalChange, (thumb.DataContext as ImageItemViewModel)?.AspectRatio);
            resizable.Width = size.Width;
            resizable.Height = size.Height;
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


        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);


        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            isSpaceHeld = false;
            isPanning = false;
            isSelecting = false;
            Mouse.OverrideCursor = null;

            if (CanvasViewport.IsMouseCaptured)
                CanvasViewport.ReleaseMouseCapture();

            // Deferred: Window.Deactivated can fire slightly BEFORE Windows finishes
            // updating GetForegroundWindow() during an Alt-Tab transition — checking
            // immediately can still see OUR window as "foreground" for a brief
            // moment, making the process check below wrongly conclude nothing
            // actually changed. Deferring to the next dispatcher cycle lets the OS
            // settle first, so the check reflects reality.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IntPtr foreground = GetForegroundWindow();
                GetWindowThreadProcessId(foreground, out uint foregroundProcessId);
                uint ourProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

                System.Diagnostics.Debug.WriteLine($"Deactivated check: foregroundProcessId={foregroundProcessId}, ourProcessId={ourProcessId}, different={foregroundProcessId != ourProcessId}");

                if (foregroundProcessId != ourProcessId)
                {
                    System.Diagnostics.Debug.WriteLine("Closing popovers due to app switch");
                    activeTextStylePicker?.ClosePopover();
                    if (((FrameworkElement)Content).FindName("BoardColorTool") is Controls.ColorPickerButton colorTool)
                        colorTool.ClosePopover();
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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

                Rect bounds = CanvasCoordinateService.SelectionBounds(selectionStartPoint, currentPos);
                SelectionBox.Margin = new Thickness(bounds.Left, bounds.Top, 0, 0);
                SelectionBox.Width = bounds.Width;
                SelectionBox.Height = bounds.Height;
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
