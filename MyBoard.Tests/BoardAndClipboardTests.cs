using MyBoard.Commands;
using MyBoard.Model;
using MyBoard.Services;
using MyBoard.ViewModel;

namespace MyBoard.Tests;

public sealed class BoardAndClipboardTests : IsolatedTest
{
    private static BoardViewModel CreateBoard() => new(Boards.Tree(1), new UndoRedoManager());

    [Fact]
    public void Cloned_tree_has_new_ids_correct_parents_and_independent_documents_blocks_and_runs()
    {
        var original = Boards.Tree();

        var clone = Assert.IsType<Board>(CanvasItemClonerService.Clone(original));

        Boards.Equal(original, clone, sameIds: false);
        var originalNote = Assert.IsType<NoteItem>(original.Items[0]);
        var clonedNote = Assert.IsType<NoteItem>(clone.Items[0]);
        Assert.NotSame(originalNote.Document, clonedNote.Document);
        Assert.NotSame(originalNote.Document.Blocks[0], clonedNote.Document.Blocks[0]);
        Assert.NotSame(originalNote.Document.Blocks[0].Runs[0], clonedNote.Document.Blocks[0].Runs[0]);
        clonedNote.Document.Blocks[0].Runs[0].Text = "changed";
        clonedNote.Document.Blocks[0].Type = NoteBlockType.QuoteBlock;
        clonedNote.Document.Blocks.Add(new NoteBlock());
        Assert.Equal("Hello 世界 👋", originalNote.Document.Blocks[0].Runs[0].Text);
        Assert.Equal(NoteBlockType.NormalHeading, originalNote.Document.Blocks[0].Type);
        Assert.Equal(2, originalNote.Document.Blocks.Count);
        Assert.IsType<Board>(clone.Items[2]).Items.Clear();
        Assert.NotEmpty(Assert.IsType<Board>(original.Items[2]).Items);
    }

    [Fact]
    public void Copy_paste_preserves_originals_relative_spacing_and_reusable_clipboard_snapshot()
    {
        var board = CreateBoard();
        board.SelectItems(board.Items.Take(2));
        var originals = board.Model.Items.Take(2).ToArray();
        board.CopySelectedItems();
        int originalCount = board.Items.Count;
        Assert.False(ClipboardService.IsCutOperation);
        Assert.Equal(originalCount, board.Model.Items.Count);

        board.PasteClipboard(300, -50);

        Assert.Equal(originalCount + 2, board.Items.Count);
        Assert.Same(originals[0], board.Model.Items[0]);
        var pasted = board.SelectedItems.Cast<ICanvasItemViewModel>().Select(vm => vm.Model).ToArray();
        Assert.Equal(originals[1].X - originals[0].X, pasted[1].X - pasted[0].X);
        Assert.Equal(originals[1].Y - originals[0].Y, pasted[1].Y - pasted[0].Y);
        Assert.Equal(320, pasted.Min(i => i.X));
        Assert.Equal(-30, pasted.Min(i => i.Y));
        Assert.All(pasted, item => Assert.DoesNotContain(item.Id, originals.Select(o => o.Id)));
        Assert.True(ClipboardService.HasContent);
        Assert.IsType<NoteItem>(pasted[0]).Document.Blocks[0].Runs[0].Text = "edited paste";
        board.PasteClipboard(0, 0);
        var next = Assert.IsType<NoteItem>(((ICanvasItemViewModel)board.SelectedItems[0]).Model);
        Assert.Equal("Hello 世界 👋", next.Document.Blocks[0].Runs[0].Text);
        Assert.NotEqual(pasted[0].Id, next.Id);
    }

    [Fact]
    public void Cut_paste_removes_originals_reparents_boards_and_clears_clipboard_after_success()
    {
        var source = CreateBoard();
        var destination = CreateBoard();
        source.SelectItems(source.Items.Take(3));
        var cut = source.SelectedItems.ToArray();
        source.CutSelectedItems();
        Assert.True(ClipboardService.IsCutOperation);
        Assert.All(cut, vm => Assert.DoesNotContain(vm, source.Items));
        Assert.Empty(source.SelectedItems);

        destination.PasteClipboard(0, 0);

        Assert.False(ClipboardService.HasContent);
        Assert.False(ClipboardService.IsCutOperation);
        var nested = Assert.IsType<BoardViewModel>(destination.SelectedItems[2]);
        Assert.Equal(destination.Model.Id, nested.Model.ParentBoardId);
        Assert.All(nested.Model.Items.OfType<Board>(), b => Assert.Equal(nested.Model.Id, b.ParentBoardId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Duplicate_keeps_originals_offsets_copies_and_selects_only_duplicates(int count)
    {
        var board = CreateBoard();
        var originals = board.Model.Items.Take(count).ToArray();
        board.SelectItems(board.Items.Take(count));
        int before = board.Items.Count;

        board.DuplicateSelectedItems();

        Assert.Equal(before + count, board.Items.Count);
        Assert.Equal(count, board.SelectedItems.Count);
        for (int i = 0; i < count; i++)
        {
            var clone = ((ICanvasItemViewModel)board.SelectedItems[i]).Model;
            Assert.NotEqual(originals[i].Id, clone.Id);
            Assert.Equal(originals[i].X + 20, clone.X);
            Assert.Equal(originals[i].Y + 20, clone.Y);
            Assert.Same(originals[i], board.Model.Items[i]);
            Assert.False(((ISelectable)board.Items[i]).IsSelected);
        }
    }

    [Fact]
    public void Single_multi_and_clear_selection_synchronize_flags_and_primary_item()
    {
        var board = CreateBoard();
        board.SelectItem(board.Items[0]);
        board.SelectItem(board.Items[1]);
        Assert.False(((ISelectable)board.Items[0]).IsSelected);
        Assert.True(((ISelectable)board.Items[1]).IsSelected);
        Assert.Same(board.Items[1], board.PrimarySelectedItem);
        Assert.Single(board.SelectedItems);

        board.SelectItems(board.Items.Take(3));
        Assert.Null(board.PrimarySelectedItem);
        Assert.All(board.Items.Take(3).Cast<ISelectable>(), item => Assert.True(item.IsSelected));
        Assert.False(((ISelectable)board.Items[3]).IsSelected);
        board.SelectItems(board.SelectedItems);
        Assert.Equal(3, board.SelectedItems.Count);
        board.ClearSelection();

        Assert.Empty(board.SelectedItems);
        Assert.Null(board.PrimarySelectedItem);
        Assert.Null(board.SelectedItem);
        Assert.All(board.Items.Cast<ISelectable>(), item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void Empty_selection_and_empty_clipboard_actions_are_safe_and_do_not_record_undo()
    {
        var history = new UndoRedoManager();
        var board = new BoardViewModel(new Board(), history);

        board.DeleteSelectedItems();
        board.DuplicateSelectedItems();
        board.CopySelectedItems();
        board.CutSelectedItems();
        board.PasteClipboard(10, 10);

        Assert.Empty(board.Items);
        Assert.Empty(board.SelectedItems);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Added_board_uses_parent_id_and_blank_title_commit_is_undoable()
    {
        var history = new UndoRedoManager();
        var parent = new BoardViewModel(new Board(), history);
        var child = parent.AddBoard();
        Assert.Equal(parent.Model.Id, child.Model.ParentBoardId);
        Assert.True(child.IsEditingTitle);
        child.Title = "Original";
        child.BeginEditingTitle();
        child.Title = " \t ";

        child.CommitTitle();

        Assert.Equal("Unnamed Board", child.Model.Title);
        Assert.False(child.IsEditingTitle);
        history.Undo();
        Assert.Equal("Original", child.Model.Title);
        history.Redo();
        Assert.Equal("Unnamed Board", child.Model.Title);
    }

    [Fact]
    public void View_model_edits_update_underlying_models_and_raise_property_notifications()
    {
        var board = CreateBoard();
        var changed = new List<string?>();
        board.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        board.Title = "Changed";
        board.Color = "#987654";
        board.X = 55;
        board.Y = -45;
        Assert.Equal(("Changed", "#987654", 55d, -45d), (board.Model.Title, board.Model.Color, board.Model.X, board.Model.Y));
        Assert.Contains("Title", changed);
        var note = Assert.IsType<NoteItemViewModel>(board.Items[0]);
        note.X = 1; note.Y = 2; note.Width = 300; note.Height = 240; note.Document = Boards.Document("new");
        Assert.Equal((1d, 2d, 300d, 240d), (note.Model.X, note.Model.Y, note.Model.Width, note.Model.Height));
        Assert.Same(note.Document, note.Model.Document);
        var image = Assert.IsType<ImageItemViewModel>(board.Items[1]);
        image.X = 3; image.Y = 4; image.Width = 500; image.Height = 100; image.FilePath = "changed.png"; image.AspectRatio = 5;
        Assert.Equal((3d, 4d, 500d, 100d, "changed.png", 5d),
            (image.Model.X, image.Model.Y, image.Model.Width, image.Model.Height, image.Model.FilePath, image.Model.AspectRatio));
    }
}
