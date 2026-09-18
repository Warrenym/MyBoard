using System.IO;
using System.Text.Json;

namespace MyBoard.Services;

internal static class AppStoragePaths
{
    private const string AppFolderName = "MyBoard";
    private static readonly string LegacyRootFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");
    private static string rootFolder;

    public static string RootFolder => rootFolder;
    public static string ImageFolder => Path.Combine(rootFolder, "Images");
    public static bool HasExplicitSelection { get; private set; }

    static AppStoragePaths()
    {
        rootFolder = ResolveRootFolder();
        Directory.CreateDirectory(rootFolder);

        if (!HasExplicitSelection && !PathsEqual(rootFolder, LegacyRootFolder))
            CopyDataIfMissing(LegacyRootFolder, rootFolder);
    }

    public static void SelectRootFolder(string folder)
    {
        string fullPath = Normalize(folder);
        Directory.CreateDirectory(fullPath);

        string json = JsonSerializer.Serialize(
            new StorageSettings { DataFolder = fullPath },
            new JsonSerializerOptions { WriteIndented = true });
        AtomicFile.Write(SettingsPath, json, SettingsPath + ".bak");

        rootFolder = fullPath;
        HasExplicitSelection = true;
    }

    public static void CopyDataIfMissing(string sourceFolder, string destinationFolder)
    {
        string source = Normalize(sourceFolder);
        string destination = Normalize(destinationFolder);
        if (PathsEqual(source, destination) || !Directory.Exists(source)) return;
        if (IsInside(destination, source) || IsInside(source, destination))
            throw new IOException("The new data folder cannot be inside the current data folder, or contain it.");

        Directory.CreateDirectory(destination);
        foreach (string sourcePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(sourcePath);
            if (extension.Equals(".lock", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath = Path.GetRelativePath(source, sourcePath);
            string destinationPath = Path.Combine(destination, relativePath);
            if (File.Exists(destinationPath)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }
    }

    private static string ResolveRootFolder()
    {
        string? selected = LoadSelectedFolder();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            HasExplicitSelection = true;
            return Normalize(selected);
        }

        string? configured = Environment.GetEnvironmentVariable("MYBOARD_DATA_FOLDER");
        if (!string.IsNullOrWhiteSpace(configured)) return Normalize(configured);

        foreach (string variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            string? oneDrive = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(oneDrive) && Directory.Exists(oneDrive))
                return Path.Combine(oneDrive, AppFolderName);
        }

        return LegacyRootFolder;
    }

    private static string? LoadSelectedFolder()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            return JsonSerializer.Deserialize<StorageSettings>(File.ReadAllText(SettingsPath))?.DataFolder;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsInside(string candidate, string parent) =>
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private sealed class StorageSettings
    {
        public string DataFolder { get; set; } = "";
    }
}
