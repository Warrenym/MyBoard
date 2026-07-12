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
            DataContext = new MainViewModel(); // Root ViewModel drives the whole window
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

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
            Point dropPosition = new(rawPosition.X / viewModel.ZoomLevel,
                                     rawPosition.Y / viewModel.ZoomLevel);


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

        // Zooms the canvas in/out on mouse wheel — no modifier key required.
        // Clamped between 20% and 300% so content can't disappear or become unusably huge.
        private void BoardCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;

            double change = e.Delta > 0 ? 0.1 : -0.1; // Scroll up = zoom in, scroll down = zoom out
            viewModel.ZoomLevel = Math.Clamp(viewModel.ZoomLevel + change, 0.2, 3.0);

            e.Handled = true; // Prevents the scroll from also trying to scroll a parent container
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

        // Fires only when clicking empty canvas space — item clicks are already marked Handled by DraggableBehavior, so they never reach this handler
        private void CanvasViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (ViewModel.MainViewModel)DataContext;
            viewModel.CurrentBoard.ClearSelection();
        }


        // Deletes the selected item on Delete/Backspace — but only when the user isn't actively typing in a TextBox, so editing note text still works normally
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            if (Keyboard.FocusedElement is TextBox) return; // Let the TextBox handle it instead

            var viewModel = (ViewModel.MainViewModel)DataContext;
            viewModel.CurrentBoard.DeleteSelectedItem();
        }
    }
}