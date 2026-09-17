using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MyBoard.Services
{
    // Handles saving/loading the entire board tree (Home and everything nested inside it).
    internal static class BoardSaveService
    {
        private static readonly string SaveFolder = AppStoragePaths.RootFolder;

        private static readonly string SaveFilePath = Path.Combine(SaveFolder, "board.json");

        // WriteIndented makes the saved file human-readable, useful for debugging early on
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        // Serializes the root board (and everything nested inside it) to disk
        public static void Save(Board rootBoard)
        {
            string json = JsonSerializer.Serialize(rootBoard, Options);
            AppStoragePaths.WriteAllTextAtomically(SaveFilePath, json);
        }

        // Loads the saved board tree, or returns null if no save file exists yet (e.g. first time running the app)
        public static Board? Load()
        {
            if (!File.Exists(SaveFilePath))
                return null;

            string json = File.ReadAllText(SaveFilePath);
            var board = JsonSerializer.Deserialize<Board>(json, Options);

            if (board != null)
            {
                MigrateLegacyNotes(board);
                ResolveImagePaths(board);
            }

            return board;
        }

        private static void ResolveImagePaths(Board board)
        {
            foreach (var item in board.Items)
            {
                if (item is ImageItem image)
                    image.FilePath = AppStoragePaths.ResolveImagePath(image.FilePath);
                else if (item is Board childBoard)
                    ResolveImagePaths(childBoard);
            }
        }

        private static void MigrateLegacyNotes(Board board)
        {
            foreach (var item in board.Items)
            {
                if (item is NoteItem note && !string.IsNullOrEmpty(note.Content) &&
                    (note.Document.Blocks.Count == 0 ||
                     note.Document.Blocks.All(b => b.Runs.Count == 0)))
                {
                    note.Document = new NoteDocument
                    {
                        Blocks = new List<NoteBlock>
                {
                    new NoteBlock
                    {
                        Type = NoteBlockType.Normal,
                        Runs = new List<NoteRun> { new NoteRun { Text = note.Content } }
                    }
                }
                    };
                }

                if (item is Board childBoard)
                    MigrateLegacyNotes(childBoard); // recurse into nested boards
            }
        }
    } 
}
