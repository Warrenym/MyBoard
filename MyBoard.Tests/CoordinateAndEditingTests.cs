using MyBoard.Model;
using MyBoard.Services;
using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Input;

namespace MyBoard.Tests;

public sealed class CoordinateAndEditingTests
{
    [Theory]
    [InlineData(1, 0, 0, 100, 200, 100, 200)]
    [InlineData(0.4, 20, -10, 100, 200, 200, 525)]
    [InlineData(2, -40, 50, -100, -200, -30, -125)]
    [InlineData(3, 10, 20, 40, 80, 10, 20)]
    public void Coordinate_conversion_accounts_for_zoom_pan_and_negative_positions(double zoom, double panX, double panY,
        double screenX, double screenY, double expectedX, double expectedY)
    {
        var screen = new Point(screenX, screenY);

        var canvas = CanvasCoordinateService.ScreenToCanvas(screen, panX, panY, zoom);

        Assert.Equal(expectedX, canvas.X, 8);
        Assert.Equal(expectedY, canvas.Y, 8);
        Assert.Equal(screen, CanvasCoordinateService.CanvasToScreen(canvas, panX, panY, zoom));
    }

    [Fact]
    public void Zoom_clamps_at_limits_and_keeps_pointer_over_the_same_canvas_position()
    {
        Assert.Equal(0.4, CanvasCoordinateService.ClampZoom(-10));
        Assert.Equal(3, CanvasCoordinateService.ClampZoom(10));
        Assert.Equal(1.7, CanvasCoordinateService.ClampZoom(1.7));
        var pointer = new Point(-42, 315);
        var before = CanvasCoordinateService.ScreenToCanvas(pointer, 40, -60, 1.2);

        foreach (double zoom in new[] { 0.4, 1.2, 2, 3 })
        {
            var pan = CanvasCoordinateService.PanForZoom(pointer, 40, -60, 1.2, zoom);
            var after = CanvasCoordinateService.ScreenToCanvas(pointer, pan.X, pan.Y, zoom);
            Assert.Equal(before.X, after.X, 8);
            Assert.Equal(before.Y, after.Y, 8);
        }
    }

    [Fact]
    public void Resize_enforces_both_minimum_dimensions_and_preserves_image_aspect_ratio()
    {
        Assert.Equal(new Size(60, 40), CanvasCoordinateService.Resize(180, 120, -1000, -1000));
        Assert.Equal(new Size(200, 150), CanvasCoordinateService.Resize(180, 120, 20, 30));

        foreach (double ratio in new[] { 0.25, 1, 4 })
        {
            var size = CanvasCoordinateService.Resize(200, 100, -1000, -1000, ratio);
            Assert.True(size.Width >= 60);
            Assert.True(size.Height >= 40);
            Assert.Equal(ratio, size.Width / size.Height, 8);
        }
    }

    [Fact]
    public void Selection_bounds_are_normalized_in_all_four_drag_directions()
    {
        var start = new Point(-20, 30);
        foreach (var end in new[] { new Point(40, 60), new Point(-80, 60), new Point(40, 0), new Point(-80, 0) })
        {
            var bounds = CanvasCoordinateService.SelectionBounds(start, end);

            Assert.Equal(60, bounds.Width);
            Assert.Equal(30, bounds.Height);
            Assert.True(bounds.Contains(start));
            Assert.True(bounds.Contains(end));
            Assert.Equal(bounds, CanvasCoordinateService.SelectionBounds(end, start));
        }
    }

    [Fact]
    public void Text_editing_and_handled_events_leave_delete_and_clipboard_shortcuts_to_the_editor()
    {
        Assert.False(EditingPolicy.CanHandleCanvasShortcut(isTyping: true, alreadyHandled: false));
        Assert.False(EditingPolicy.CanHandleCanvasShortcut(isTyping: false, alreadyHandled: true));
        Assert.True(EditingPolicy.CanHandleCanvasShortcut(isTyping: false, alreadyHandled: false));
        Assert.All(new[] { Key.C, Key.X, Key.V, Key.Z, Key.Y, Key.D }, key => Assert.True(EditingPolicy.IsCanvasControlShortcut(key)));
        Assert.False(EditingPolicy.IsCanvasControlShortcut(Key.A));
        Assert.True(EditingPolicy.CanRename(1, true));
        Assert.False(EditingPolicy.CanRename(0, false));
        Assert.False(EditingPolicy.CanRename(1, false));
        Assert.False(EditingPolicy.CanRename(2, true));
    }

    [Fact]
    public void Enter_resets_headings_and_code_to_normal_but_continues_quotes()
    {
        foreach (var type in Enum.GetValues<NoteBlockType>())
            Assert.Equal(type == NoteBlockType.QuoteBlock ? NoteBlockType.QuoteBlock : NoteBlockType.Normal,
                EditingPolicy.BlockAfterEnter(type));
    }

    [Fact]
    public void Empty_note_placeholder_is_hidden_while_editing_or_when_block_has_a_style()
    {
        var note = new NoteItemViewModel(new NoteItem());
        Assert.True(note.IsEmpty);
        Assert.True(note.ShowPlaceholder);
        note.IsEditing = true;
        Assert.False(note.ShowPlaceholder);
        note.IsEditing = false;
        note.Document = new NoteDocument { Blocks = [new NoteBlock { Type = NoteBlockType.LargeHeading }] };
        Assert.True(note.IsEmpty);
        Assert.False(note.ShowPlaceholder);
        note.Document = Boards.Document();
        Assert.False(note.IsEmpty);
        Assert.False(note.ShowPlaceholder);
    }
}
