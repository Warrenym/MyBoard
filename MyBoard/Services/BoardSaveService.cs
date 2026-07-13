using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MyBoard.Services
{
    // Handles saving/loading the entire board tree (Home and everything nested inside it) as a single JSON file in AppData.
    internal static class BoardSaveService
    {
        private static readonly string SaveFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyBoard");

        private static readonly string SaveFilePath = Path.Combine(SaveFolder, "board.json");

        // WriteIndented makes the saved file human-readable, useful for debugging early on
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        // Serializes the root board (and everything nested inside it) to disk
        public static void Save(Board rootBoard)
        {
            Directory.CreateDirectory(SaveFolder); // Ensure the folder exists on first save
            string json = JsonSerializer.Serialize(rootBoard, Options);
            File.WriteAllText(SaveFilePath, json);
        }

        // Loads the saved board tree, or returns null if no save file exists yet (e.g. first time running the app)
        public static Board? Load()
        {
            if (!File.Exists(SaveFilePath))
                return null;

            string json = File.ReadAllText(SaveFilePath);
            return JsonSerializer.Deserialize<Board>(json, Options);
        }
    } 
}
