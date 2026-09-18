using System.IO;
using System.Text;

namespace MyBoard.Services;

internal static class AtomicFile
{
    // A sibling temporary file keeps the final rename on the same volume.
    internal static void Write(string path, string contents, string? backupPath = null,
        Action<string>? beforeCommit = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Encoding.UTF8.GetBytes(contents));
                stream.Flush(flushToDisk: true);
            }
            beforeCommit?.Invoke(temporaryPath);
            if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
