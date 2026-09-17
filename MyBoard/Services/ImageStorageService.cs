using System.IO;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Windows.Media.Imaging;

namespace MyBoard.Services
{
    // Handles saving images from any drag source into the app-managed, synced folder.
    internal static class ImageStorageService
    {
		// Images live beside board.json so OneDrive syncs the complete board.
		private static readonly string ImageFolder = AppStoragePaths.ImageFolder;

        static ImageStorageService()
        {
            Directory.CreateDirectory(ImageFolder); // Ensure the folder exists on first run
        }

        // Case 1: File Explorer drop — copies the existing file into our managed folder
        public static string CopyFile(string sourcePath)
        {
            string extension = Path.GetExtension(sourcePath);
            string fileName = $"{Guid.NewGuid()}{extension}";
            string fullPath = Path.Combine(ImageFolder, fileName);

            File.Copy(sourcePath, fullPath, overwrite: true);
            return fullPath;
        }

        // Case 2: Raw bitmap data drop — encodes the pixels to a PNG file
        public static string SaveBitmap(BitmapSource bitmap)
        {
            string fileName = $"{Guid.NewGuid()}.png";
            string fullPath = Path.Combine(ImageFolder, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);

            return fullPath;
        }

        // Case 3: Browser drop — downloads the image from its URL
        public static async Task<string> DownloadImageAsync(string url)
        {
            using var httpClient = new HttpClient();
            byte[] data = await httpClient.GetByteArrayAsync(url);

            string extension = Path.GetExtension(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(extension) || extension.Length > 5)
                extension = ".jpg"; // Fallback when the URL has no clean file extension

            string fileName = $"{Guid.NewGuid()}{extension}";
            string fullPath = Path.Combine(ImageFolder, fileName);

            await File.WriteAllBytesAsync(fullPath, data);
            return fullPath;
        }

    }
}
