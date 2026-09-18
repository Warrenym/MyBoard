using MyBoard.Model;
using System.Windows;

namespace MyBoard.Services
{
    internal static class ClipboardService
    {
        private const string MyBoardClipboardFormat = "MyBoard.CanvasItems";

        public static List<ICanvasItem> Items { get; private set; } = new();
        public static bool IsCutOperation { get; private set; }

        public static bool HasContent => Items.Count > 0;
        internal static Action ClipboardMarker { get; set; } = MarkSystemClipboard;

        public static void Clear()
        {
            Items.Clear();
            IsCutOperation = false;
        }

        public static void SetCopy(IEnumerable<ICanvasItem> items)
        {
            Items = items.Select(CanvasItemClonerService.Clone).ToList();
            IsCutOperation = false;
            ClipboardMarker();
        }

        public static void SetCut(IEnumerable<ICanvasItem> items)
        {
            Items = items.Select(CanvasItemClonerService.Clone).ToList();
            IsCutOperation = true;
            ClipboardMarker();
        }

        public static bool OwnsSystemClipboard(IDataObject data) =>
            HasContent && data.GetDataPresent(MyBoardClipboardFormat);

        private static void MarkSystemClipboard()
        {
            if (!HasContent) return;

            try
            {
                var data = new DataObject();
                data.SetData(MyBoardClipboardFormat, "1");
                Clipboard.SetDataObject(data, true);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Another application can briefly lock the Windows clipboard.
                // The in-app clipboard still remains usable for this session.
            }
        }
    }
}
