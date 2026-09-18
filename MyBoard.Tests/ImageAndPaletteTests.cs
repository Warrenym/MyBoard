using MyBoard.Commands;
using MyBoard.Model;
using MyBoard.Services;
using MyBoard.ViewModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyBoard.Tests;

public sealed class ImageAndPaletteTests : IsolatedTest
{
    [Fact]
    public void Supported_image_extensions_are_case_insensitive_and_ignore_url_query_and_fragment()
    {
        foreach (string extension in new[] { "png", "jpg", "jpeg", "gif", "bmp", "webp" })
        {
            Assert.True(ImageStorageService.IsSupportedImage("image." + extension));
            Assert.True(ImageStorageService.IsSupportedImage("image." + extension.ToUpperInvariant()));
            Assert.True(ImageStorageService.IsSupportedImage($"https://example.test/image.{extension.ToUpperInvariant()}?token=abc#anchor"));
        }
        Assert.False(ImageStorageService.IsSupportedImage("readme.txt"));
        Assert.False(ImageStorageService.IsSupportedImage("https://example.test/page?image=.png"));
        Assert.False(ImageStorageService.IsSupportedImage("no-extension"));
    }

    [Fact]
    public void Copy_file_uses_unique_managed_filenames_and_keeps_source_bytes()
    {
        string source = FileInTest("source.PNG");
        byte[] bytes = [1, 2, 3, 4];
        File.WriteAllBytes(source, bytes);
        string managed = FileInTest("Images");

        string first = ImageStorageService.CopyFile(source, managed);
        string second = ImageStorageService.CopyFile(source, managed);

        Assert.NotEqual(first, second);
        Assert.Equal(managed, Path.GetDirectoryName(first));
        Assert.Equal(".PNG", Path.GetExtension(first));
        Assert.Equal(bytes, File.ReadAllBytes(first));
        Assert.Equal(bytes, File.ReadAllBytes(second));
        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Throws<FileNotFoundException>(() => ImageStorageService.CopyFile(FileInTest("absent.png"), managed));
        Assert.Equal(2, Directory.GetFiles(managed).Length);
    }

    [Fact]
    public void Bitmap_encoding_creates_valid_png_and_board_uses_natural_aspect_ratio() => Sta.Run(() =>
    {
        var bitmap = BitmapSource.Create(4, 2, 96, 96, PixelFormats.Bgra32, null, new byte[32], 16);

        string path = ImageStorageService.SaveBitmap(bitmap, FileInTest("Images"));

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, File.ReadAllBytes(path).Take(8));
        using (var input = File.OpenRead(path))
        {
            var decoded = new PngBitmapDecoder(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Assert.Equal(4, decoded.Frames[0].PixelWidth);
            Assert.Equal(2, decoded.Frames[0].PixelHeight);
        }
        var board = new BoardViewModel(new Board(), new UndoRedoManager());
        board.AddImage(path, -10, 25);
        var image = Assert.IsType<ImageItem>(Assert.Single(board.Model.Items));
        Assert.Equal((2d, 200d, 100d, -10d, 25d), (image.AspectRatio, image.Width, image.Height, image.X, image.Y));
        File.Delete(path); // Loading dimensions must not retain an open file handle.
        Assert.False(File.Exists(path));
    });

    [Fact]
    public void Missing_and_corrupt_images_fall_back_to_square_dimensions() => Sta.Run(() =>
    {
        string corrupt = FileInTest("corrupt.png");
        File.WriteAllText(corrupt, "not an image");
        var board = new BoardViewModel(new Board(), new UndoRedoManager());

        board.AddImage(FileInTest("missing.png"), 1, 2);
        board.AddImage(corrupt, 3, 4);

        Assert.All(board.Model.Items.Cast<ImageItem>(), item =>
        {
            Assert.Equal(1, item.AspectRatio);
            Assert.Equal(item.Width, item.Height);
        });
    });

    [Fact]
    public void Download_uses_injected_http_client_and_decodes_content_regardless_of_url_extension() => Sta.Run(() =>
    {
        string seed = ImageStorageService.SaveBitmap(BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[8], 8), Folder);
        byte[] png = File.ReadAllBytes(seed);
        Uri? requested = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
        }));

        string saved = ImageStorageService.DownloadImageAsync("https://example.test/photo.jpeg?download=1", client, FileInTest("Images")).GetAwaiter().GetResult();

        Assert.Equal("https://example.test/photo.jpeg?download=1", requested!.OriginalString);
        Assert.Equal(".png", Path.GetExtension(saved));
        using var stream = File.OpenRead(saved);
        Assert.Equal(2, new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0].PixelWidth);
        string dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
        Assert.True(File.Exists(ImageStorageService.SaveDataUri(dataUri, FileInTest("Images"))));
    });

    [Fact]
    public void Failed_truncated_or_corrupt_downloads_never_leave_partial_managed_files() => Sta.Run(() =>
    {
        string folder = FileInTest("Images");
        Directory.CreateDirectory(folder);
        foreach (var response in new[]
        {
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new InterruptedContent() }
        })
        {
            using var client = new HttpClient(new StubHandler(_ => response));

            var failure = Record.Exception(() => ImageStorageService.DownloadImageAsync("https://example.test/file", client, folder).GetAwaiter().GetResult());

            Assert.NotNull(failure);
            Assert.Empty(Directory.GetFiles(folder));
        }
        Assert.Throws<InvalidDataException>(() => ImageStorageService.SaveDataUri("data:image/png,not-base64", folder));
        Assert.Empty(Directory.GetFiles(folder));
    });

    [Fact]
    public void Palette_loads_defaults_without_a_save_and_round_trips_saved_and_recent_colors()
    {
        var palette = new ColorPaletteViewModel(Folder);
        Assert.Equal(12, palette.DefaultColors.Count);
        Assert.Empty(palette.SavedColors);
        Assert.Empty(palette.RecentlyPicked);
        palette.AddToSaved("#abcdef");
        palette.RecordRecentlyPicked("#123456");

        var loaded = new ColorPaletteViewModel(Folder);

        Assert.Equal(palette.DefaultColors, loaded.DefaultColors);
        Assert.Equal(new[] { "#ABCDEF" }, loaded.SavedColors);
        Assert.Equal(new[] { "#123456" }, loaded.RecentlyPicked);
    }

    [Fact]
    public void Recent_colors_move_duplicates_to_front_and_cap_history_at_ten()
    {
        var palette = new ColorPaletteViewModel(Folder);
        for (int i = 0; i < 12; i++) palette.RecordRecentlyPicked($"#{i:X6}");
        Assert.Equal(Enumerable.Range(2, 10).Reverse().Select(i => $"#{i:X6}"), palette.RecentlyPicked);

        palette.RecordRecentlyPicked("#00000a");
        palette.AddToSaved("#aabbcc");
        palette.AddToSaved("#AABBCC");

        Assert.Equal("#00000A", palette.RecentlyPicked[0]);
        Assert.Equal("#00000B", palette.RecentlyPicked[1]);
        Assert.Equal(10, palette.RecentlyPicked.Count);
        Assert.Equal(10, palette.RecentlyPicked.Distinct().Count());
        Assert.Single(palette.SavedColors);
        Assert.Equal(palette.RecentlyPicked, new ColorPaletteViewModel(Folder).RecentlyPicked);
    }

    [Fact]
    public void Invalid_color_inputs_are_rejected_without_mutating_or_saving_palette()
    {
        var palette = new ColorPaletteViewModel(Folder);
        foreach (string invalid in new[] { "", "garbage", "#GGGGGG", "123456", "#12345" })
        {
            Assert.Throws<ArgumentException>(() => palette.RecordRecentlyPicked(invalid));
            Assert.Throws<ArgumentException>(() => palette.AddToSaved(invalid));
        }
        Assert.Empty(palette.SavedColors);
        Assert.Empty(palette.RecentlyPicked);
        Assert.False(File.Exists(FileInTest("palette.json")));
    }

    [Fact]
    public void Corrupt_palette_falls_back_without_rewriting_file_and_default_row_cannot_be_changed()
    {
        File.WriteAllText(FileInTest("palette.json"), "broken");

        var palette = new ColorPaletteViewModel(Folder);

        Assert.Empty(palette.SavedColors);
        Assert.Equal("broken", File.ReadAllText(FileInTest("palette.json")));
        Assert.False(palette.TryMoveColor("#123456", PaletteRow.Saved, PaletteRow.Default));
        Assert.False(palette.TryMoveColor("#123456", PaletteRow.Default, PaletteRow.Recent));
        Assert.Equal(12, palette.DefaultColors.Count);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class InterruptedContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(new byte[] { 137, 80, 78, 71 });
            throw new IOException("Connection interrupted mid-response");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
