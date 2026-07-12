using MyBoard.Services;
using MyBoard.ViewModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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
            Point dropPosition = e.GetPosition(BoardCanvas);


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

    }
}