using MyBoard.Model;
using MyBoard.Services;
using System.IO;
using System.Text.Json;

namespace MyBoard.Tests;

public sealed class PersistenceTests : IsolatedTest
{
    [Fact]
    public void Save_load_preserves_all_properties_and_concrete_types_through_four_levels()
    {
        var original = Boards.Tree();
        var storage = new BoardSaveService(Folder);

        storage.Save(original);
        var loaded = new BoardSaveService(Folder).Load();

        Boards.Equal(original, Assert.IsType<Board>(loaded));
        Assert.Empty(Directory.GetFiles(Folder, "*.tmp"));
    }

    [Fact]
    public void Empty_boards_and_empty_notes_round_trip()
    {
        var board = new Board();
        var storage = new BoardSaveService(Folder);
        storage.Save(board);
        Boards.Equal(board, storage.Load()!);

        board.Items.Add(new NoteItem());
        board.Items.Add(new Board { ParentBoardId = board.Id });
        storage.Save(board);

        Boards.Equal(board, storage.Load()!);
    }

    [Theory]
    [InlineData("old text", "")]
    [InlineData("", "")]
    [InlineData("old text", ",\"Document\":null")]
    [InlineData("old text", ",\"Document\":{\"Blocks\":[]}")]
    [InlineData("old text", ",\"Document\":{\"Blocks\":[{\"Runs\":[{\"Text\":\"\"}]}]}")]
    public void Legacy_notes_migrate_recursively_and_remain_migrated_after_resaving(string content, string document)
    {
        File.WriteAllText(FileInTest("board.json"), $$"""
            {"Items":[{"$type":"board","Items":[{"$type":"note","Content":{{JsonSerializer.Serialize(content)}}{{document}}}]}]}
            """);
        var storage = new BoardSaveService(Folder);

        var loaded = storage.Load()!;
        var note = Assert.IsType<NoteItem>(Assert.IsType<Board>(Assert.Single(loaded.Items)).Items.Single());

        var block = Assert.Single(note.Document.Blocks);
        Assert.Equal(NoteBlockType.Normal, block.Type);
        Assert.Equal(content, string.Concat(block.Runs.Select(r => r.Text)));
        Assert.Null(note.Content);
        storage.Save(loaded);
        Boards.Equal(loaded, storage.Load()!);
        note.Document = NoteDocument.CreateEmpty();
        storage.Save(loaded);
        var again = Assert.IsType<NoteItem>(Assert.IsType<Board>(storage.Load()!.Items.Single()).Items.Single());
        Assert.Empty(Assert.Single(again.Document.Blocks).Runs);
    }

    [Fact]
    public void Populated_modern_document_wins_over_legacy_content()
    {
        var board = new Board { Items = [new NoteItem { Content = "stale", Document = Boards.Document("modern") }] };
        File.WriteAllText(FileInTest("board.json"), JsonSerializer.Serialize(board));

        var note = Assert.IsType<NoteItem>(new BoardSaveService(Folder).Load()!.Items.Single());

        Assert.Equal("modern", note.Document.Blocks[0].Runs[0].Text);
        Assert.Null(note.Content);
    }

    [Fact]
    public void Missing_save_returns_null_and_missing_properties_use_model_defaults()
    {
        var storage = new BoardSaveService(Folder);
        Assert.Null(storage.Load());
        File.WriteAllText(FileInTest("board.json"), "{\"Items\":[{\"$type\":\"note\"}]}");

        var board = storage.Load()!;

        Assert.Equal("Untitled Board", board.Title);
        Assert.NotEqual(Guid.Empty, board.Id);
        var note = Assert.IsType<NoteItem>(Assert.Single(board.Items));
        Assert.Equal(180, note.Width);
        Assert.Equal(120, note.Height);
        Assert.Single(note.Document.Blocks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"Items\":[{\"$type\":\"alien\"}]}")]
    [InlineData("{\"Items\":[{}]}")]
    [InlineData("{\"Items\":[{\"$type\":\"note\",\"Width\":-1}]}")]
    [InlineData("{\"Items\":null}")]
    public void Invalid_saves_fail_explicitly_and_cannot_be_silently_overwritten(string contents)
    {
        string path = FileInTest("board.json");
        File.WriteAllText(path, contents);
        var storage = new BoardSaveService(Folder);

        var loadFailure = Record.Exception(() => storage.Load());
        var saveFailure = Record.Exception(() => storage.Save(new Board()));

        Assert.True(loadFailure is JsonException or InvalidDataException or NotSupportedException);
        Assert.True(saveFailure is JsonException or InvalidDataException or NotSupportedException);
        Assert.Equal(contents, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Folder, "*.tmp"));
    }

    [Fact]
    public void Nonfinite_coordinates_and_invalid_dimensions_are_rejected_before_writing()
    {
        var storage = new BoardSaveService(Folder);
        storage.Save(new Board { Title = "safe" });
        string original = File.ReadAllText(FileInTest("board.json"));
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<InvalidDataException>(() => storage.Save(new Board { X = invalid }));
            Assert.Throws<InvalidDataException>(() => storage.Save(new Board { Items = [new NoteItem { Height = invalid }] }));
        }
        Assert.Throws<InvalidDataException>(() => storage.Save(new Board { Items = [new ImageItem { Width = -1 }] }));
        Assert.Equal(original, File.ReadAllText(FileInTest("board.json")));
    }

    [Fact]
    public void Second_save_backs_up_previous_version_and_corrupt_primary_recovers_without_overwriting_backup()
    {
        var storage = new BoardSaveService(Folder);
        storage.Save(new Board { Title = "first" });
        string first = File.ReadAllText(FileInTest("board.json"));
        storage.Save(new Board { Title = "second" });
        Assert.Equal(first, File.ReadAllText(FileInTest("board.json.bak")));
        File.WriteAllText(FileInTest("board.json"), "interrupted sync");

        var recovered = storage.Load()!;
        Assert.Equal("first", recovered.Title);
        Assert.Equal("interrupted sync", File.ReadAllText(FileInTest("board.json")));
        storage.Save(recovered);

        Assert.Equal(first, File.ReadAllText(FileInTest("board.json.bak")));
        Boards.Equal(recovered, storage.Load()!);
        Assert.Equal("interrupted sync", File.ReadAllText(Assert.Single(Directory.GetFiles(Folder, "board.json.corrupt.*"))));
    }

    [Fact]
    public void Future_version_is_not_recovered_from_backup_or_overwritten()
    {
        var storage = new BoardSaveService(Folder);
        storage.Save(new Board());
        storage.Save(new Board());
        const string future = "{\"FormatVersion\":999,\"Title\":\"future\"}";
        File.WriteAllText(FileInTest("board.json"), future);
        var reader = new BoardSaveService(Folder);

        Assert.Throws<UnsupportedBoardVersionException>(() => reader.Load());
        Assert.Throws<UnsupportedBoardVersionException>(() => reader.Save(new Board()));

        Assert.Equal(future, File.ReadAllText(FileInTest("board.json")));
    }

    [Fact]
    public void Failed_commit_keeps_primary_and_backup_and_cleans_temporary_file()
    {
        var storage = new BoardSaveService(Folder);
        storage.Save(new Board { Title = "first" });
        storage.Save(new Board { Title = "second" });
        string primary = File.ReadAllText(FileInTest("board.json"));
        string backup = File.ReadAllText(FileInTest("board.json.bak"));
        bool reachedCommit = false;
        var failing = new BoardSaveService(Folder, temporary =>
        {
            reachedCommit = true;
            Assert.True(File.Exists(temporary));
            Assert.Equal(primary, File.ReadAllText(FileInTest("board.json")));
            throw new IOException("Simulated interruption before atomic replacement");
        });
        failing.Load();

        Assert.Throws<IOException>(() => failing.Save(new Board()));

        Assert.True(reachedCommit);
        Assert.Equal(primary, File.ReadAllText(FileInTest("board.json")));
        Assert.Equal(backup, File.ReadAllText(FileInTest("board.json.bak")));
        Assert.Empty(Directory.GetFiles(Folder, "*.tmp"));
    }

    [Fact]
    public void Unwritable_destination_reports_failure_without_creating_a_save()
    {
        File.WriteAllText(FileInTest("not-a-directory"), "occupied");
        var storage = new BoardSaveService(FileInTest("not-a-directory"));

        Assert.ThrowsAny<IOException>(() => storage.Save(new Board()));

        Assert.Equal("occupied", File.ReadAllText(FileInTest("not-a-directory")));
        Assert.False(File.Exists(FileInTest("board.json")));
    }

    [Fact]
    public void Stale_instances_and_concurrent_writers_cannot_overwrite_newer_data()
    {
        var first = new BoardSaveService(Folder);
        var second = new BoardSaveService(Folder);
        first.Load();
        second.Load();
        first.Save(new Board { Title = "winner" });

        Assert.Throws<IOException>(() => second.Save(new Board { Title = "stale" }));
        second.Load();
        using (var locked = new FileStream(FileInTest("board.json.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => second.Save(new Board()));

        Assert.Equal("winner", first.Load()!.Title);
        second.Save(new Board { Title = "reloaded" });
        Assert.Equal("reloaded", first.Load()!.Title);
    }

    [Fact]
    public void Image_relocation_uses_only_the_explicit_storage_folder_and_preserves_missing_paths()
    {
        Directory.CreateDirectory(FileInTest("Images"));
        string local = Path.Combine(Folder, "Images", "shared.png");
        File.WriteAllBytes(local, [1, 2, 3]);
        var board = new Board { Items = [new ImageItem { FilePath = "Z:/other-machine/shared.png" }, new ImageItem { FilePath = "absent.png" }] };
        var storage = new BoardSaveService(Folder);
        storage.Save(board);

        var loaded = storage.Load()!;

        Assert.Equal(local, Assert.IsType<ImageItem>(loaded.Items[0]).FilePath);
        Assert.Equal("absent.png", Assert.IsType<ImageItem>(loaded.Items[1]).FilePath);
        using var json = JsonDocument.Parse(File.ReadAllText(FileInTest("board.json")));
        Assert.Equal(BoardSaveService.CurrentVersion, json.RootElement.GetProperty("FormatVersion").GetInt32());
    }
}
