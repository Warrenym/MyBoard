using MyBoard.Model;
using MyBoard.Services;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MyBoard.Tests;

public abstract class IsolatedTest : IDisposable
{
    protected string Folder { get; } = Path.Combine(Path.GetTempPath(), "MyBoard.Tests", Guid.NewGuid().ToString("N"));
    private readonly Action previousMarker;

    protected IsolatedTest()
    {
        Directory.CreateDirectory(Folder);
        previousMarker = ClipboardService.ClipboardMarker;
        ClipboardService.ClipboardMarker = () => { };
        ClipboardService.Clear();
    }

    protected string FileInTest(string name) => Path.Combine(Folder, name);

    public void Dispose()
    {
        ClipboardService.Clear();
        ClipboardService.ClipboardMarker = previousMarker;
        Directory.Delete(Folder, recursive: true);
        GC.SuppressFinalize(this);
    }
}

internal static class Sta
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA test exceeded its timeout.");
        failure?.Throw();
    }
}

internal static class Boards
{
    public static NoteDocument Document(string text = "Hello 世界 👋") => new()
    {
        Blocks = [new NoteBlock
        {
            Type = NoteBlockType.NormalHeading,
            Runs = [new NoteRun { Text = text, Bold = true, Italic = true, Underline = true,
                Strikethrough = true, InlineCode = true, TextColorKey = "red", HighlightColorKey = "blue",
                LinkUrl = "https://example.test/note" }, new NoteRun { Text = " second run" }]
        }, new NoteBlock { Type = NoteBlockType.CodeBlock, Runs = [new NoteRun { Text = "a\n b\t" }] }]
    };

    public static Board Tree(int depth = 3, Guid? parent = null)
    {
        var board = new Board { Title = $"Level {depth}", Color = "#123456", X = -18.25, Y = 72.5, ParentBoardId = parent };
        board.Items.Add(new NoteItem { X = 14, Y = -16, Width = 340, Height = 155, Document = Document() });
        board.Items.Add(new ImageItem { X = 60, Y = 88, Width = 210, Height = 140, AspectRatio = 1.5, FilePath = "missing-image.png" });
        if (depth > 0) board.Items.Add(Tree(depth - 1, board.Id));
        board.Items.Add(new Board { ParentBoardId = board.Id, Title = "Empty" });
        return board;
    }

    public static void Equal(ICanvasItem expected, ICanvasItem actual, bool sameIds = true)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        if (sameIds) Assert.Equal(expected.Id, actual.Id);
        else Assert.NotEqual(expected.Id, actual.Id);
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        switch (expected)
        {
            case Board board:
                var other = Assert.IsType<Board>(actual);
                Assert.Equal(board.Title, other.Title);
                Assert.Equal(board.Color, other.Color);
                if (sameIds) Assert.Equal(board.ParentBoardId, other.ParentBoardId);
                Assert.Equal(board.Items.Count, other.Items.Count);
                for (int i = 0; i < board.Items.Count; i++) Equal(board.Items[i], other.Items[i], sameIds);
                if (!sameIds)
                    foreach (var child in other.Items.OfType<Board>()) Assert.Equal(other.Id, child.ParentBoardId);
                break;
            case NoteItem note:
                var otherNote = Assert.IsType<NoteItem>(actual);
                Assert.Equal(note.Width, otherNote.Width);
                Assert.Equal(note.Height, otherNote.Height);
                // Structured model serialization is stable and compares every persisted formatting field.
                Assert.Equal(JsonSerializer.Serialize(note.Document), JsonSerializer.Serialize(otherNote.Document));
                Assert.Equal(note.Content, otherNote.Content);
                break;
            case ImageItem image:
                var otherImage = Assert.IsType<ImageItem>(actual);
                Assert.Equal(image.Width, otherImage.Width);
                Assert.Equal(image.Height, otherImage.Height);
                Assert.Equal(image.AspectRatio, otherImage.AspectRatio);
                Assert.Equal(image.FilePath, otherImage.FilePath);
                break;
        }
    }
}
