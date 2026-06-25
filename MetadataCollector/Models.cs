namespace MetadataCollector
{
    /// <summary>One raw (un-normalized) metadata value as a provider reported it, plus whether the
    /// pipeline has a rule/alias to promote it. The core "what does extraction look like" record.</summary>
    public sealed class RawRow
    {
        public string File { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string? SubResource { get; set; }
        public bool Mapped { get; set; }
    }

    /// <summary>A canonical attribute the pipeline would store, after promotion/aliasing.</summary>
    public sealed class CanonicalRow
    {
        public string File { get; set; } = "";
        public string Group { get; set; } = "";
        public string Attribute { get; set; } = "";
        public string Value { get; set; } = "";
        public string? Unit { get; set; }
        public string SourceProvider { get; set; } = "";
        public string? SubResource { get; set; }
    }

    /// <summary>An unmapped (provider, namespace, key) aggregated across the run — the worklist for
    /// improving normalization rules / aliases. Ranked by how many files carry it.</summary>
    public sealed class UnmappedRow
    {
        public string Provider { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string Key { get; set; } = "";
        public int Files { get; set; }
        public int Occurrences { get; set; }
        public string SampleValue { get; set; } = "";
    }

    /// <summary>Per-file outcome and counts.</summary>
    public sealed class FileRow
    {
        public string File { get; set; } = "";
        public string Extension { get; set; } = "";
        public long SizeBytes { get; set; }
        public int TikaItems { get; set; }
        public int ExifToolItems { get; set; }
        public int MetaCliItems { get; set; }
        public int CanonicalItems { get; set; }
        public int UnmappedItems { get; set; }
        public string Status { get; set; } = "";
        public string? Error { get; set; }
    }
}
