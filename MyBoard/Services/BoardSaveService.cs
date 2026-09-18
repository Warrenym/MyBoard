using MyBoard.Model;
using System.IO;
using System.Text.Json;

namespace MyBoard.Services;

// One instance per editing session. Explicit paths never initialize user storage.
internal sealed class BoardSaveService
{
    internal const int CurrentVersion = 1;
    private readonly string path;
    private readonly string storageFolder;
    private readonly Action<string>? beforeCommit;
    private string? loadedContents;
    private bool loaded;
    private bool recovered;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public BoardSaveService(string? storageFolder = null, Action<string>? beforeCommit = null)
    {
        this.storageFolder = Path.GetFullPath(storageFolder ?? AppStoragePaths.RootFolder);
        path = Path.Combine(this.storageFolder, "board.json");
        this.beforeCommit = beforeCommit;
    }

    public bool HasExternalChanges()
    {
        if (!loaded) return false;
        string? current = File.Exists(path) ? File.ReadAllText(path) : null;
        return current != loadedContents;
    }

    public Board? Load()
    {
        string? contents = File.Exists(path) ? File.ReadAllText(path) : null;
        recovered = false;
        Board? board;
        try { board = contents is null ? null : Deserialize(contents); }
        catch (Exception ex) when ((ex is JsonException or InvalidDataException or NotSupportedException)
                                   && File.Exists(path + ".bak"))
        {
            board = Deserialize(File.ReadAllText(path + ".bak"));
            recovered = true;
        }
        loadedContents = contents;
        loaded = true;
        return board;
    }

    public void Save(Board rootBoard)
    {
        Validate(rootBoard);
        Board portableBoard = JsonSerializer.Deserialize<Board>(JsonSerializer.Serialize(rootBoard, Options), Options)!;
        MakeImagePathsPortable(portableBoard);
        var json = JsonSerializer.SerializeToNode(portableBoard, Options)!.AsObject();
        json["FormatVersion"] = CurrentVersion;
        string contents = json.ToJsonString(Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Keeping the lock-file name avoids a delete/recreate race between processes.
        using var writeLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string? existing = File.Exists(path) ? File.ReadAllText(path) : null;
        if (loaded && existing != loadedContents)
            throw new IOException("The board was changed by another instance. Reload before saving.");
        if (existing == contents)
        {
            loadedContents = contents;
            loaded = true;
            recovered = false;
            return;
        }
        if (existing is not null && !recovered)
            _ = Deserialize(existing); // Never overwrite an unreadable or future-format save.
        // Preserve the corrupt primary as evidence; never replace the known-good backup with it.
        string backupPath = recovered ? path + ".corrupt." + Guid.NewGuid().ToString("N") : path + ".bak";
        AtomicFile.Write(path, contents, backupPath, beforeCommit);
        if (existing is not null && !recovered)
            CreateTimestampedBackup(existing);
        loadedContents = contents;
        loaded = true;
        recovered = false;
    }

    private Board Deserialize(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("A board save must contain an object.");
        if (parsed.RootElement.TryGetProperty("FormatVersion", out var version))
        {
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number < 1)
                throw new InvalidDataException("Invalid board format version.");
            if (number > CurrentVersion) throw new UnsupportedBoardVersionException(number);
        }
        Board board = JsonSerializer.Deserialize<Board>(json, Options)
            ?? throw new InvalidDataException("The save does not contain a board.");
        Migrate(board);
        Validate(board);
        return board;
    }

    private void Migrate(Board board)
    {
        if (board.Items is null) throw new InvalidDataException("Board items cannot be null.");
        foreach (var item in board.Items)
        {
            if (item is NoteItem note)
            {
                note.Document ??= NoteDocument.CreateEmpty();
                if (!string.IsNullOrEmpty(note.Content) && note.Document.Blocks is not null &&
                    (note.Document.Blocks.Count == 0 || note.Document.Blocks.All(b =>
                        b?.Runs is not null && b.Runs.All(r => r is not null && string.IsNullOrEmpty(r.Text)))))
                    note.Document = new NoteDocument { Blocks = [new NoteBlock { Runs = [new NoteRun { Text = note.Content }] }] };
                // Do not resurrect legacy text after the user intentionally empties the document.
                note.Content = null;
            }
            else if (item is ImageItem image && !string.IsNullOrWhiteSpace(image.FilePath))
            {
                string candidate;
                if (!Path.IsPathRooted(image.FilePath) &&
                    image.FilePath.StartsWith("Images" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    candidate = Path.GetFullPath(Path.Combine(storageFolder, image.FilePath));
                else
                    candidate = Path.Combine(storageFolder, "Images", Path.GetFileName(image.FilePath));

                if (File.Exists(candidate) || !Path.IsPathRooted(image.FilePath) &&
                    image.FilePath.StartsWith("Images" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    image.FilePath = candidate;
            }
            else if (item is Board child) Migrate(child);
        }
    }

    private void MakeImagePathsPortable(Board board)
    {
        string imageFolder = Path.Combine(storageFolder, "Images");
        foreach (var item in board.Items)
        {
            if (item is ImageItem image && !string.IsNullOrWhiteSpace(image.FilePath))
            {
                string fullPath = Path.IsPathRooted(image.FilePath)
                    ? Path.GetFullPath(image.FilePath)
                    : Path.GetFullPath(Path.Combine(storageFolder, image.FilePath));
                if (fullPath.StartsWith(imageFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    image.FilePath = Path.Combine("Images", Path.GetFileName(fullPath));
            }
            else if (item is Board child) MakeImagePathsPortable(child);
        }
    }

    private void CreateTimestampedBackup(string contents)
    {
        string backupFolder = Path.Combine(storageFolder, "Backups");
        Directory.CreateDirectory(backupFolder);
        string backupPath = Path.Combine(backupFolder, $"board-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(backupPath, contents);

        foreach (string oldBackup in Directory.EnumerateFiles(backupFolder, "board-*.json")
                     .OrderByDescending(File.GetCreationTimeUtc).Skip(10))
            File.Delete(oldBackup);
    }

    private static void Validate(Board board)
    {
        if (board.Items is null || board.Title is null || board.Color is null)
            throw new InvalidDataException("Board properties cannot be null.");
        foreach (var item in board.Items.Prepend(board))
        {
            if (item is null || !double.IsFinite(item.X) || !double.IsFinite(item.Y))
                throw new InvalidDataException("Item coordinates must be finite.");
            if (item is NoteItem note)
            {
                ValidateSize(note.Width, note.Height);
                if (note.Document?.Blocks is null || note.Document.Blocks.Any(b => b?.Runs is null || b.Runs.Any(r => r?.Text is null)))
                    throw new InvalidDataException("Invalid note document.");
            }
            if (item is ImageItem image)
            {
                ValidateSize(image.Width, image.Height);
                if (!double.IsFinite(image.AspectRatio) || image.AspectRatio <= 0 || image.FilePath is null)
                    throw new InvalidDataException("Invalid image properties.");
            }
            if (item is Board child && !ReferenceEquals(child, board)) Validate(child);
        }
    }

    private static void ValidateSize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new InvalidDataException("Item dimensions must be finite and positive.");
    }
}

internal sealed class UnsupportedBoardVersionException(int version)
    : IOException($"Board format version {version} is newer than this application supports.");
