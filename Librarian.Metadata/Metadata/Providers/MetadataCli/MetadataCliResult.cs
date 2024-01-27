using Newtonsoft.Json;

namespace Librarian.Metadata.Providers.MetadataCli
{
    public class MetadataCliResult
    {
        public string? Parser { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public MetadataCliStream[]? Streams { get; set; }

        public MetadataCliChapter[]? Chapters { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object>? Properties { get; set; }

        public long? BitRate { get; set; }

        //public long? DurationTb { get; set; }

        //public long? StartTimeTb { get; set; }
    }

    public enum MetadataCliStreamType
    {
        Video,
        Audio,
        Data,
        Subtitle,
        Attachment,
        Unknown
    }

    public class MetadataCliStream
    {
        public long Id { get; set; }

        public long? Index { get; set; }

        public MetadataCliStreamType? Type { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public double? AspectRatio { get; set; }

        public long? BitRate { get; set; }

        public long? BitsPerSample { get; set; }

        public int? Channels { get; set; }

        public string? Codec { get; set; }

        public double? Duration { get; set; }

        public double? FrameRate { get; set; }

        public double? RealFrameRate { get; set; }

        public long? Frames { get; set; }

        public long? Width { get; set; }

        public long? Height { get; set; }

        public long? SampleRate { get; set; }

        public double? StartTime { get; set; }

        // The following fields aren't in the metadata-cli output, they are parsed from the dictionary

        public string? Title { get; set; }
    }

    public class MetadataCliChapter
    {
        public long Id { get; set; }

        public double? Start { get; set; }

        public double? End { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        // The following fields aren't in the metadata-cli output, they are parsed from the dictionary

        public string? Title { get; set; }
    }
}
