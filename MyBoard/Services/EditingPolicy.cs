using MyBoard.Model;
using System.Windows.Input;

namespace MyBoard.Services;

internal static class EditingPolicy
{
    public static bool CanHandleCanvasShortcut(bool isTyping, bool alreadyHandled) => !isTyping && !alreadyHandled;

    public static bool CanRename(int selectedCount, bool selectedIsBoard) => selectedCount == 1 && selectedIsBoard;

    // Plain Enter ends a heading/code block; quotes continue on the next paragraph.
    public static NoteBlockType BlockAfterEnter(NoteBlockType current) =>
        current == NoteBlockType.QuoteBlock ? NoteBlockType.QuoteBlock : NoteBlockType.Normal;

    public static bool IsCanvasControlShortcut(Key key) => key is Key.Z or Key.Y or Key.C or Key.X or Key.V or Key.D;
}
