using System.Text;

namespace MetadataParser.Container
{
    public class RiffParser
    {
        private readonly Stream? stream;
        private readonly BinaryReader? reader;

        public RiffParser(Stream stream)
        {
            this.stream = stream;
            reader = new BinaryReader(stream);
        }

        public bool Probe()
        {
            if (reader == null || stream == null)
                throw new Exception("Object is already disposed!");

            stream.Seek(0, SeekOrigin.Begin);
            bool result = Encoding.ASCII.GetString(reader.ReadBytes(4)) == "RIFF";

            stream.Seek(0, SeekOrigin.Begin);
            return result;
        }

        public RiffChunk ReadChunks()
        {
            if (reader == null || stream == null)
                throw new Exception("Object is already disposed!");

            RiffChunk chunk = new()
            {
                Id = Encoding.ASCII.GetString(reader.ReadBytes(4)),
                ChunkSize = reader.ReadUInt32(),
                ChunkOffset = stream!.Position,
            };

            if (chunk.Id == "RIFF" || chunk.Id == "LIST")
            {
                chunk.Type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                chunk.SubChunks = new List<RiffChunk>();
                while (stream.Position < chunk.ChunkOffset + Pad(chunk.ChunkSize))
                    chunk.SubChunks.Add(ReadChunks());
            }
            else
            {
                stream.Seek(Pad(chunk.ChunkSize), SeekOrigin.Current);
            }

            return chunk;
        }

        private static uint Pad(uint size)
        {
            return size + (size % 2);
        }
    }
}
