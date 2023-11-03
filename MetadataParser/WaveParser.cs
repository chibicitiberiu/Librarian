using MetadataParser.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetadataParser
{
    public class WaveParser : IMetadataParser
    {
        public IEnumerable<string, string, object> ParseMetadata(Stream inputStream)
        {
            RiffParser riffParser = new (inputStream);
            var rootChunk = riffParser.ReadChunks();
            if (rootChunk.Type != "WAVE")
                throw new Exception("Not a WAV file!");

            return WalkChunks(rootChunk, inputStream);
        }

        private IEnumerable<(string, string, object)> WalkChunks(RiffChunk chunk, Stream inputStream)
        {
            switch (chunk.Type)
            {
                case "fmt ":
                    {
                        WaveFormat fmt = ParseFmtChunk(chunk, inputStream);
                        yield return ("Wave", "Format Tag", fmt.FormatTag);
                        yield return ("Audio", "Channels", fmt.Channels);
                        yield return ("Audio", "Bitrate", 8 * fmt.AverageBytesPerSecond);
                        yield return ("Audio", "Samples/second", fmt.SamplesPerSecond);
                        yield return ("Audio", "Bits/sample", fmt.BitsPerSample);
                    }
                    break;
            }

            foreach (var subChunk in chunk.SubChunks)
            {
                foreach (var ret in WalkChunks(subChunk, inputStream))
                    yield return ret;
            }
        }

        private WaveFormat ParseFmtChunk(RiffChunk chunk, Stream inputStream)
        {
            var reader = new BinaryReader(inputStream);
            inputStream.Seek(chunk.ChunkOffset, SeekOrigin.Begin);
            return new()
            {
                FormatTag = (WaveFormatType)reader.ReadUInt16(),
                Channels = reader.ReadUInt16(),
                SamplesPerSecond = reader.ReadUInt32(),
                AverageBytesPerSecond = reader.ReadUInt32(),
                BlockAlign = reader.ReadUInt16(),
                BitsPerSample = reader.ReadUInt16()
            };
        }
    }
}
