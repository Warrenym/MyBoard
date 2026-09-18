using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MyBoard.Services
{
    // Clones any ICanvasItem (Note, Image, or Board — including nested contents)
    internal static class CanvasItemClonerService
    {
        private static readonly JsonSerializerOptions Options = new();

        public static ICanvasItem Clone(ICanvasItem original)
        {
            string json = JsonSerializer.Serialize(original, typeof(ICanvasItem), Options);
            var clone = (ICanvasItem)JsonSerializer.Deserialize(json, typeof(ICanvasItem), Options)!;
            AssignNewIds(clone);
            return clone;
        }

        // Recursively reassigns Id — matters for Boards, since a cloned board's children need new identities too, not just the board itself
        private static void AssignNewIds(ICanvasItem item)
        {
            switch (item)
            {
                case NoteItem note:
                    note.Id = Guid.NewGuid();
                    break;
                case ImageItem image:
                    image.Id = Guid.NewGuid();
                    break;
                case Board board:
                    board.Id = Guid.NewGuid();
                    foreach (var child in board.Items)
                    {
                        AssignNewIds(child);
                        if (child is Board childBoard) childBoard.ParentBoardId = board.Id;
                    }
                    break;
            }
        }
    }
}
