using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MyBoard.Services
{
    internal class AppStoragePaths
    {
        public static readonly string RootFolder = ResolveRootFolder();

        private static string ResolveRootFolder()
        {
            // Windows sets this environment variable automatically once
            // OneDrive is installed and signed in
            string? oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");

            if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
                return Path.Combine(oneDrivePath, "MyBoard");

            // Fallback: normal local AppData, same as before this change
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyBoard");
        }
    }
}
