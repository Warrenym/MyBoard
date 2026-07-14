using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Services
{
    internal static class ClipboardService
    {
        public static List<ICanvasItem> Items { get; private set; } = new();
        public static bool IsCutOperation { get; private set; }

        public static bool HasContent => Items.Count > 0;

        public static void SetCopy(IEnumerable<ICanvasItem> items)
        {
            Items = items.Select(CanvasItemClonerService.Clone).ToList();
            IsCutOperation = false;
        }

        public static void SetCut(IEnumerable<ICanvasItem> items)
        {
            Items = items.Select(CanvasItemClonerService.Clone).ToList();
            IsCutOperation = true;
        }
    }
}
