using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace MyBoard.Services
{
    // Handles saving images from drag-and-drop and clipboard sources into the
    // app-managed, synced folder.
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

        public static string SaveDataUri(string dataUri)
        {
            int commaIndex = dataUri.IndexOf(',');
            if (commaIndex < 0 || !dataUri[..commaIndex].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The image data URI is not base64 encoded.");

            byte[] data = Convert.FromBase64String(dataUri[(commaIndex + 1)..]);
            return SaveEncodedImage(data);
        }

        // Case 3: Browser drop — downloads the image from its URL
        public static async Task<string> DownloadImageAsync(string url)
        {
            using var httpClient = new HttpClient();
            byte[] data = await httpClient.GetByteArrayAsync(url);
            return SaveEncodedImage(data);
        }

        private static string SaveEncodedImage(byte[] data)
        {
            using var input = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(
                input,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("The supplied data does not contain an image.");

            return SaveBitmap(decoder.Frames[0]);
        }

    }
}
