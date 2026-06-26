using System.Collections.Generic;

namespace Librarian.ViewModels
{
    /// <summary>Backs the admin/control panel: library stats plus the (previously API-only) maintenance
    /// actions — reindex, associate, renormalize, rebuild search, checksum, and unmapped-key inspection.</summary>
    public class AdminViewModel
    {
        public int TotalFiles { get; set; }
        public int NeedsUpdating { get; set; }
        public int Incomplete { get; set; }
        public int Items { get; set; }
        public int Collections { get; set; }
        public int DuplicateSets { get; set; }
        public string ChecksumMode { get; set; } = "Off";

        /// <summary>Most common raw (namespace, key) pairs that have no normalization rule yet.</summary>
        public IReadOnlyList<UnmappedKey> UnmappedKeys { get; set; } = new List<UnmappedKey>();
        public int UnmappedTotal { get; set; }

        /// <summary>One-shot result banner from the last action (via TempData).</summary>
        public string? Message { get; set; }
    }

    public record UnmappedKey(string Namespace, string Key, int Count, string? SampleValue);
}
