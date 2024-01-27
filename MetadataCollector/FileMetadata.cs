using System.Text.RegularExpressions;

internal class FileMetadata
{
    public string File { get; }
    public string Extension { get; }
    public string MimeType { get; }
    public string Property { get; }
    public string PropertyNormalized { get; }
    public string? Value { get; }

    public FileMetadata(string file, string extension, string mimeType, string property, object? value)
    {
        File = file;
        Extension = extension;
        MimeType = mimeType;
        Property = property;
        PropertyNormalized = Regex.Replace(property, "streams\\[\\d+\\]", "streams[n]");
        Value = value?.ToString() ?? "null";
    }

    public override bool Equals(object? obj)
    {
        return obj is FileMetadata other &&
               File == other.File &&
               Extension == other.Extension &&
               MimeType == other.MimeType &&
               Property == other.Property &&
               EqualityComparer<object?>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(File, Extension, MimeType, Property, Value);
    }
}