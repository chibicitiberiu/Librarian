internal class FileInfo
{
    public string File { get; }
    public string FileName { get; }
    public string Extension { get; }
    public string MimeType { get; }
    public bool Relevant { get; }
    public bool CouldCollect { get; }

    public FileInfo(string file, string fileName, string extension, string mimeType, bool relevant, bool couldCollect)
    {
        File = file;
        FileName = fileName;
        Extension = extension;
        MimeType = mimeType;
        Relevant = relevant;
        CouldCollect = couldCollect;
    }

    public override bool Equals(object? obj)
    {
        return obj is FileInfo other &&
               File == other.File &&
               FileName == other.FileName &&
               Extension == other.Extension &&
               MimeType == other.MimeType &&
               Relevant == other.Relevant &&
               CouldCollect == other.CouldCollect;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(File, FileName, Extension, MimeType, Relevant, CouldCollect);
    }
}
