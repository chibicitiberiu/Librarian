using System.Security.Cryptography;
using System.Text;
using Librarian.Services;
using Xunit;

namespace Test
{
    /// <summary>
    /// Correctness of the staged-hashing primitives. The full pass (size → prefix → full escalation,
    /// duplicate grouping) is DB-backed and covered by live verification; these guard the bytes-in →
    /// SHA-256-out contract, including the prefix truncation that the dedup pre-filter relies on.
    /// </summary>
    public class ChecksumServiceTests
    {
        private static async Task<string> TempFileAsync(byte[] data)
        {
            string path = Path.GetTempFileName();
            await File.WriteAllBytesAsync(path, data);
            return path;
        }

        [Fact]
        public async Task HashFileAsync_is_sha256_of_whole_file()
        {
            byte[] data = Encoding.UTF8.GetBytes("hello world");
            string path = await TempFileAsync(data);
            try
            {
                Assert.Equal(SHA256.HashData(data), await ChecksumService.HashFileAsync(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task HashPrefixAsync_hashes_only_the_first_bytes()
        {
            byte[] data = new byte[1000];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)i;
            string path = await TempFileAsync(data);
            try
            {
                byte[] prefix = await ChecksumService.HashPrefixAsync(path, 100);
                Assert.Equal(SHA256.HashData(data.AsSpan(0, 100).ToArray()), prefix);
                // Two files sharing a 100-byte prefix but differing later collide on the prefix hash
                // (so the pass escalates to the full hash) — that's the staged design.
                Assert.NotEqual(await ChecksumService.HashFileAsync(path), prefix);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task HashPrefixAsync_hashes_whole_file_when_shorter_than_prefix()
        {
            byte[] data = Encoding.UTF8.GetBytes("short");
            string path = await TempFileAsync(data);
            try
            {
                Assert.Equal(SHA256.HashData(data), await ChecksumService.HashPrefixAsync(path, 4096));
            }
            finally { File.Delete(path); }
        }
    }
}
