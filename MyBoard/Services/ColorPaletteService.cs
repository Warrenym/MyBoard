using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MyBoard.Services
{
    // Persists the user's custom palette (Saved + Recently Picked) to a single app-wide JSON file
    static class ColorPaletteService
    {
        private static readonly string SaveFolder = AppStoragePaths.RootFolder;

        private static readonly string SaveFilePath = Path.Combine(SaveFolder, "palette.json");

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static ColorPaletteData Load()
        {
            if (!File.Exists(SaveFilePath))
                return new ColorPaletteData();

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                return JsonSerializer.Deserialize<ColorPaletteData>(json, Options) ?? new ColorPaletteData();
            }
            catch
            {
                // Corrupt or unreadable file — fall back to empty rather than crashing the app
                return new ColorPaletteData();
            }
        }

        public static void Save(ColorPaletteData data)
        {
            string json = JsonSerializer.Serialize(data, Options);
            AppStoragePaths.WriteAllTextAtomically(SaveFilePath, json);
        }
    }
}
