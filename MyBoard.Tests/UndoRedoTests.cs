using MyBoard.Commands;
using MyBoard.Model;
using MyBoard.ViewModel;
using System.Collections.ObjectModel;

namespace MyBoard.Tests;

public sealed class UndoRedoTests
{
    [Fact]
    public void Add_execute_undo_redo_restores_the_same_model_and_view_model()
    {
        var models = new ObservableCollection<ICanvasItem>();
        var views = new ObservableCollection<object>();
        var note = new NoteItem();
        var vm = new NoteItemViewModel(note);
        var command = new AddItemCommand(models, views, note, vm);

        Cycle(command, () => { Assert.Empty(models); Assert.Empty(views); },
            () => { Assert.Same(note, Assert.Single(models)); Assert.Same(vm, Assert.Single(views)); });
    }

    [Fact]
    public void Delete_execute_undo_redo_preserves_item_order_even_with_reversed_selection()
    {
        ICanvasItem[] original = [new NoteItem(), new ImageItem(), new Board(), new NoteItem()];
        object[] viewModels = [new object(), new object(), new object(), new object()];
        var models = new ObservableCollection<ICanvasItem>(original);
        var views = new ObservableCollection<object>(viewModels);
        var command = new DeleteItemsCommand(models, views, [(original[2], viewModels[2]), (original[0], viewModels[0])]);

        Cycle(command, () => { Assert.Equal(original, models); Assert.Equal(viewModels, views); },
            () => { Assert.Equal(new[] { original[1], original[3] }, models); Assert.Equal(new[] { viewModels[1], viewModels[3] }, views); });
    }

    [Fact]
    public void Move_execute_undo_redo_restores_exact_positions_for_every_item()
    {
        var first = new NoteItemViewModel(new NoteItem { X = -5.25, Y = 19 });
        var second = new ImageItemViewModel(new ImageItem { X = 25, Y = 8.5 });
        var command = new MoveItemsCommand([(first, -5.25, 19, 40.5, 90), (second, 25, 8.5, 60, -10)]);

        Cycle(command, () => Assert.Equal((-5.25, 19d, 25d, 8.5), (first.Model.X, first.Model.Y, second.Model.X, second.Model.Y)),
            () => Assert.Equal((40.5, 90d, 60d, -10d), (first.Model.X, first.Model.Y, second.Model.X, second.Model.Y)));
    }

    [Fact]
    public void Resize_execute_undo_redo_restores_exact_dimensions()
    {
        var item = new NoteItemViewModel(new NoteItem { Width = 180.5, Height = 120.25 });
        var command = new ResizeItemCommand(item, item.Width, item.Height, 444.5, 333.25);

        Cycle(command, () => Assert.Equal((180.5, 120.25), (item.Model.Width, item.Model.Height)),
            () => Assert.Equal((444.5, 333.25), (item.Model.Width, item.Model.Height)));
    }

    [Fact]
    public void Rename_execute_undo_redo_restores_exact_title()
    {
        var board = new BoardViewModel(new Board { Title = "Old 👋" }, new UndoRedoManager());
        var command = new RenameCommand(board, board.Title, "New title");

        Cycle(command, () => Assert.Equal("Old 👋", board.Model.Title), () => Assert.Equal("New title", board.Model.Title));
    }

    [Fact]
    public void Color_execute_undo_redo_restores_exact_color()
    {
        var board = new BoardViewModel(new Board { Color = "#123456" }, new UndoRedoManager());
        var command = new ColorChangeCommand(board, board.Color, "#ABCDEF");

        Cycle(command, () => Assert.Equal("#123456", board.Model.Color), () => Assert.Equal("#ABCDEF", board.Model.Color));
    }

    [Fact]
    public void History_flags_and_lifo_order_follow_execute_undo_and_redo()
    {
        var history = new UndoRedoManager();
        var log = new List<string>();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        history.Undo(); history.Redo();
        Assert.Empty(log);

        history.Do(new Probe(log, "A")); history.Do(new Probe(log, "B"));
        Assert.True(history.CanUndo); Assert.False(history.CanRedo);
        history.Undo();
        Assert.True(history.CanUndo); Assert.True(history.CanRedo);
        history.Undo();
        Assert.False(history.CanUndo); Assert.True(history.CanRedo);
        history.Redo(); history.Redo();

        Assert.Equal(new[] { "+A", "+B", "-B", "-A", "+A", "+B" }, log);
        Assert.True(history.CanUndo); Assert.False(history.CanRedo);
    }

    [Fact]
    public void New_action_after_undo_discards_redo_and_record_does_not_execute_again()
    {
        var history = new UndoRedoManager();
        var log = new List<string>();
        history.Do(new Probe(log, "old"));
        history.Undo();
        var alreadyApplied = new Probe(log, "new");
        alreadyApplied.Execute();

        history.Record(alreadyApplied);
        history.Redo();

        Assert.Equal(new[] { "+old", "-old", "+new" }, log);
        Assert.False(history.CanRedo);
        history.Undo(); history.Redo();
        Assert.Equal(new[] { "+old", "-old", "+new", "-new", "+new" }, log);
    }

    private static void Cycle(IUndoableCommand command, Action before, Action after)
    {
        before();
        command.Execute(); after();
        command.Undo(); before();
        command.Execute(); after();
    }

    private sealed class Probe(List<string> log, string name) : IUndoableCommand
    {
        public void Execute() => log.Add("+" + name);
        public void Undo() => log.Add("-" + name);
    }
}
