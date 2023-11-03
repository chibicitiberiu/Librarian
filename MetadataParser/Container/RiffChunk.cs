namespace MetadataParser.Container
{
    public class RiffChunk
    {
        public string Id { get; set; } = null!;
        public uint ChunkSize { get; set; }
        public long ChunkOffset { get; set; }
        public string Type { get; set; } = null!;
        public List<RiffChunk> SubChunks { get; set; } = null!;
    }
}
