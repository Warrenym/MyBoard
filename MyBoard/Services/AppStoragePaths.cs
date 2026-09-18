using System.IO;

namespace MyBoard.Services
{
    internal static class AppStoragePaths
    {
        private const string AppFolderName = "MyBoard";
        private static readonly string LegacyRootFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);

        public static string RootFolder { get; } = ResolveRootFolder();
        public static string ImageFolder => Path.Combine(RootFolder, "Images");

        static AppStoragePaths()
        {
            Directory.CreateDirectory(RootFolder);

            if (!PathsEqual(RootFolder, LegacyRootFolder))
                MigrateLegacyData();
        }

        private static string ResolveRootFolder()
        {
            // This optional override is useful if Windows exposes more than one
            // OneDrive account and the board should live in a specific one.
            string? configuredFolder = Environment.GetEnvironmentVariable("MYBOARD_DATA_FOLDER");
            if (!string.IsNullOrWhiteSpace(configuredFolder))
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredFolder));

            // The generic variable normally points at the user's active OneDrive.
            // The other two cover machines that only expose an account-specific
            // variable (consumer or work/school).
            string[] oneDriveVariables = ["OneDrive", "OneDriveConsumer", "OneDriveCommercial"];
            foreach (string variable in oneDriveVariables)
            {
                string? oneDrivePath = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(oneDrivePath) && Directory.Exists(oneDrivePath))
                    return Path.Combine(oneDrivePath, AppFolderName);
            }

            // Keep the app usable when OneDrive is not installed or signed in.
            return LegacyRootFolder;
        }

        public static string ResolveImagePath(string savedPath)
        {
            if (string.IsNullOrWhiteSpace(savedPath) || File.Exists(savedPath))
                return savedPath;

            // Older saves contain an absolute path from the PC that wrote them.
            // Image names are GUIDs, so remapping by file name is both portable
            // and unambiguous inside the synced Images folder.
            string candidate = Path.Combine(ImageFolder, Path.GetFileName(savedPath));
            return File.Exists(candidate) ? candidate : savedPath;
        }

        public static void WriteAllTextAtomically(string path, string contents)
        {
            AtomicFile.Write(path, contents);
        }

        private static void MigrateLegacyData()
        {
            if (!Directory.Exists(LegacyRootFolder))
                return;

            CopyIfMissing(Path.Combine(LegacyRootFolder, "board.json"), Path.Combine(RootFolder, "board.json"));
            CopyIfMissing(Path.Combine(LegacyRootFolder, "palette.json"), Path.Combine(RootFolder, "palette.json"));

            string legacyImages = Path.Combine(LegacyRootFolder, "Images");
            if (!Directory.Exists(legacyImages))
                return;

            Directory.CreateDirectory(ImageFolder);
            foreach (string sourcePath in Directory.EnumerateFiles(legacyImages))
                CopyIfMissing(sourcePath, Path.Combine(ImageFolder, Path.GetFileName(sourcePath)));
        }

        private static void CopyIfMissing(string sourcePath, string destinationPath)
        {
            if (File.Exists(sourcePath) && !File.Exists(destinationPath))
                File.Copy(sourcePath, destinationPath);
        }

        private static bool PathsEqual(string left, string right) =>
            string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }
}
