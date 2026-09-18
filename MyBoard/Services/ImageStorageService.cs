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
        private static string GetImageFolder(string? storageFolder)
        {
            string folder = storageFolder ?? AppStoragePaths.ImageFolder;
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static bool IsSupportedImage(string path)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                path = uri.AbsolutePath;
            return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

        // Case 1: File Explorer drop — copies the existing file into our managed folder
        public static string CopyFile(string sourcePath, string? storageFolder = null)
        {
            string extension = Path.GetExtension(sourcePath);
            string fileName = $"{Guid.NewGuid()}{extension}";
            string fullPath = Path.Combine(GetImageFolder(storageFolder), fileName);

            File.Copy(sourcePath, fullPath, overwrite: true);
            return fullPath;
        }

        // Case 2: Raw bitmap data drop — encodes the pixels to a PNG file
        public static string SaveBitmap(BitmapSource bitmap, string? storageFolder = null)
        {
            string fileName = $"{Guid.NewGuid()}.png";
            string fullPath = Path.Combine(GetImageFolder(storageFolder), fileName);

            try
            {
                using var stream = new FileStream(fullPath, FileMode.CreateNew);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            catch
            {
                File.Delete(fullPath);
                throw;
            }

            return fullPath;
        }

        public static string SaveDataUri(string dataUri, string? storageFolder = null)
        {
            int commaIndex = dataUri.IndexOf(',');
            if (commaIndex < 0 || !dataUri[..commaIndex].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The image data URI is not base64 encoded.");

            byte[] data = Convert.FromBase64String(dataUri[(commaIndex + 1)..]);
            return SaveEncodedImage(data, storageFolder);
        }

        // Case 3: Browser drop — downloads the image from its URL
        public static async Task<string> DownloadImageAsync(string url)
        {
            using var httpClient = new HttpClient();
            return await DownloadImageAsync(url, httpClient);
        }

        public static async Task<string> DownloadImageAsync(string url, HttpClient httpClient, string? storageFolder = null)
        {
            byte[] data = await httpClient.GetByteArrayAsync(url);
            return SaveEncodedImage(data, storageFolder);
        }

        private static string SaveEncodedImage(byte[] data, string? storageFolder)
        {
            using var input = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(
                input,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("The supplied data does not contain an image.");

            return SaveBitmap(decoder.Frames[0], storageFolder);
        }

    }
}
