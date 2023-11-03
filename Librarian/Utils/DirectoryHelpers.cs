using System.IO;

namespace Librarian.Utils
{
    public static class DirectoryHelpers
    {
        public static void CopyRecursively(string source, string destinationDirectory, bool overwrite = false)
        {
            var sourceInfo = new DirectoryInfo(source);
            CopyRecursively(sourceInfo, destinationDirectory, overwrite);
        }

        private static void CopyRecursively(DirectoryInfo source, string destinationDirectory, bool overwrite = false)
        {
            // TODO: handle overwriting files
            string copiedDir = Path.Combine(destinationDirectory, source.Name);
            Directory.CreateDirectory(copiedDir);

            foreach (var dir in source.EnumerateDirectories())
            {
                CopyRecursively(dir, copiedDir, overwrite);
            }

            foreach (var file in source.EnumerateFiles())
            {
                file.CopyTo(copiedDir, overwrite);
            }
        }

        public static void Copy(string source, string destinationDirectory, bool overwrite = false)
        {
            if (Directory.Exists(source))
            {
                CopyRecursively(source, destinationDirectory, overwrite);
            }
            else
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(source));
                File.Copy(source, destinationFile, overwrite);
            }
        }

        public static void Move(string source, string destinationDirectory, bool overwrite = false)
        {
            source = source.TrimEnd('/', '\\');
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
            if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
            }
            else
            {
                File.Move(source, destination, overwrite);
            }
        }

        public static void DeleteRecursively(string directory)
        {
            DeleteRecursively(new DirectoryInfo(directory));
        }

        private static void DeleteRecursively(DirectoryInfo directory)
        {
            foreach (var dir in directory.GetDirectories())
            {
                DeleteRecursively(dir);
            }

            foreach (var file in directory.GetFiles())
            {
                file.Delete();
            }

            directory.Delete();
        }

        public static void Delete(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Exists)
                DeleteRecursively(dirInfo);

            else
                File.Delete(path);
        }
    }
}
