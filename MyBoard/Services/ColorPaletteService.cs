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
        private static string SaveFilePath(string? folder) => Path.Combine(folder ?? AppStoragePaths.RootFolder, "palette.json");

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static ColorPaletteData Load(string? storageFolder = null)
        {
            string path = SaveFilePath(storageFolder);
            if (!File.Exists(path))
                return new ColorPaletteData();

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ColorPaletteData>(json, Options) ?? new ColorPaletteData();
            }
            catch
            {
                // Corrupt or unreadable file — fall back to empty rather than crashing the app
                return new ColorPaletteData();
            }
        }

        public static void Save(ColorPaletteData data, string? storageFolder = null)
        {
            string json = JsonSerializer.Serialize(data, Options);
            AtomicFile.Write(SaveFilePath(storageFolder), json);
        }
    }
}
