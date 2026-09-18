using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using MyBoard.Services;

namespace MyBoard.ViewModel
{
    // Identifies which row a color belongs to
    public enum PaletteRow
    {
        Default,
        Saved,
        Recent
    }


    // Owns the app-wide color palette
    partial class ColorPaletteViewModel : ObservableObject
    {
        private const int MaxRecentlyPicked = 10;

        // Fixed baseline palette — never modified at runtime, so no persistence needed for it
        public ObservableCollection<string> DefaultColors { get; } = new()
        {
            "#F44336", "#E91E63", "#9C27B0", "#673AB7",
            "#3F51B5", "#2196F3", "#009688", "#4CAF50",
            "#FFEB3B", "#FF9800", "#795548", "#607D8B"
        };

        public ObservableCollection<string> SavedColors { get; } = new();
        public ObservableCollection<string> RecentlyPicked { get; } = new();


        private readonly string? storageFolder;

        public ColorPaletteViewModel(string? storageFolder = null)
        {
            this.storageFolder = storageFolder;
            var data = ColorPaletteService.Load(storageFolder);
            foreach (var color in data.SavedColors) SavedColors.Add(color);
            foreach (var color in data.RecentlyPicked) RecentlyPicked.Add(color);
        }


        // Call this every time a color is actually applied via the picker
        // (not on every drag-tick — the caller decides when a "pick" is final, same pattern as how undo/redo only records once per completed edit)
        public void RecordRecentlyPicked(string hex)
        {
            hex = NormalizeColor(hex);
            // Move to front if it already exists, rather than allowing duplicates
            if (RecentlyPicked.Contains(hex))
                RecentlyPicked.Remove(hex);

            RecentlyPicked.Insert(0, hex);

            while (RecentlyPicked.Count > MaxRecentlyPicked)
                RecentlyPicked.RemoveAt(RecentlyPicked.Count - 1);

            Persist();
        }


        // Adds a color to the Saved row — used both by "save this color" from the picker, and by drag-and-drop moves in edit mode later
        public void AddToSaved(string hex)
        {
            hex = NormalizeColor(hex);
            if (!SavedColors.Contains(hex))
                SavedColors.Add(hex);

            Persist();
        }


        public void RemoveFromSaved(string hex)
        {
            SavedColors.Remove(hex);
            Persist();
        }


        public void RemoveFromRecentlyPicked(string hex)
        {
            RecentlyPicked.Remove(hex);
            Persist();
        }


        // Central place enforcing your movement rules —
        // the UI's drag-and-drop handler (step 5) will call this and let it decide what's allowed, rather than duplicating the rule-checking in the View.
        public bool TryMoveColor(string hex, PaletteRow from, PaletteRow to)
        {
            // Default is permanently fixed — nothing can ever be dropped into it
            if (to == PaletteRow.Default)
                return false;

            // Nothing can be demoted into Recently Picked from a curated row
            if (to == PaletteRow.Recent && from != PaletteRow.Recent)
                return false;

            if (to == PaletteRow.Saved && !SavedColors.Contains(hex))
                SavedColors.Add(hex);

            // Only remove from the source if it's actually being MOVED (not copied from Default)
            if (from == PaletteRow.Saved && to != PaletteRow.Saved)
                SavedColors.Remove(hex);

            if (from == PaletteRow.Recent && to != PaletteRow.Recent)
                RecentlyPicked.Remove(hex);

            Persist();
            return true;
        }


        // Reorders a color within the same row — moves it to a specific index rather than removing/re-adding at the end
        public void ReorderWithinRow(PaletteRow row, string hex, int newIndex)
        {
            var collection = row == PaletteRow.Saved ? SavedColors : RecentlyPicked;
            int oldIndex = collection.IndexOf(hex);
            if (oldIndex == -1 || oldIndex == newIndex) return;

            collection.Move(oldIndex, Math.Clamp(newIndex, 0, collection.Count - 1));
            Persist();
        }


        private void Persist()
        {
            ColorPaletteService.Save(new Model.ColorPaletteData
            {
                SavedColors = SavedColors.ToList(),
                RecentlyPicked = RecentlyPicked.ToList()
            }, storageFolder);
        }

        private static string NormalizeColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
                throw new ArgumentException("Use a #RRGGBB or #AARRGGBB color.", nameof(hex));
            return hex.ToUpperInvariant();
        }
    }
}
