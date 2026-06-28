using Librarian.Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Librarian.Services
{
    public class FileService
    {
        public string BasePath { get; }

        public string AppDataPath { get; }

        public FileService(IConfiguration config)
        {
            // Store the roots without a trailing separator so containment checks and GetRelativePath
            // behave consistently (a trailing slash made the root's own relative path resolve to "..",
            // which tripped the traversal guard below and aborted indexing).
            BasePath = NormalizeRoot(PathUtils.GetCanonicalPath(config["BaseDirectory"]!));
            AppDataPath = NormalizeRoot(PathUtils.GetCanonicalPath(config["AppDataDirectory"]!));
        }

        private static string NormalizeRoot(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public string Resolve(string relativePath)
        {
            // Combine then normalize lexically (resolves any "." / ".." without touching the filesystem).
            string absPath = Path.GetFullPath(Path.Combine(BasePath, relativePath ?? string.Empty))
                                 .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Boundary-correct containment: the path must be the root itself or live under it (with a
            // separator). A plain StartsWith would both miss "/lib/.." escapes and falsely reject a
            // sibling like "/library-backup" that shares the root's textual prefix.
            if (absPath != BasePath &&
                !absPath.StartsWith(BasePath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException($"Path traversal! (resolved '{relativePath}' to '{absPath}', outside '{BasePath}')");

            return absPath;
        }

        public string GetRelativePath(string absPath)
        {
            string relPath = Path.GetRelativePath(BasePath, absPath);
            return relPath.Replace("\\", "/");
        }

        public string GetRelativePath(FileSystemInfo info)
        {
            string relPath = Path.GetRelativePath(BasePath, info.FullName);
            return relPath.Replace("\\", "/");
        }

        public bool IsRoot(string absPath)
        {
            string relPath = Path.GetRelativePath(BasePath, absPath);
            return string.IsNullOrEmpty(relPath) || relPath == ".";
        }

        public bool IsInBaseDirectory(string absPath)
        {
            absPath = PathUtils.GetCanonicalPath(absPath);
            return absPath.StartsWith(BasePath);
        }

        public string GetAppDataFile(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            else return Path.Combine(AppDataPath, path);
        }

        public void MoveFiles(string[] files, string destination)
        {
            string destinationAbsPath = Resolve(destination);

            foreach (var file in files)
            {
                var absPath = Resolve(file);
                DirectoryHelpers.Move(absPath, destinationAbsPath);
            }
        }

        public void CopyFiles(string[] files, string destination)
        {
            string destinationAbsPath = Resolve(destination);

            foreach (var file in files)
            {
                var absPath = Resolve(file);
                DirectoryHelpers.Copy(absPath, destinationAbsPath);
            }
        }

        /// <summary>
        /// Rename file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="newName">New name not including path</param>
        public void RenameFile(string file, string newName)
        {
            var absPath = Resolve(file);
            string newPath = Path.Combine(Path.GetDirectoryName(absPath)!, newName);

            if (Directory.Exists(absPath))
                Directory.Move(absPath, newPath);
            else
                File.Move(absPath, newPath);
        }

        public void DeleteFile(string relPath)
        {
            var absPath = Resolve(relPath);
            DirectoryHelpers.Delete(absPath);
        }
    }
}
