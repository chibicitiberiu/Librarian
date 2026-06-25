namespace Librarian.Model
{
    /// <summary>
    /// What a file is within its containing "work" (the keystone of plan.md). Most files are
    /// <see cref="Primary"/> — they stand on their own. A <see cref="Sidecar"/> carries metadata for a
    /// sibling primary (e.g. a Calibre <c>metadata.opf</c>, an <c>.nfo</c>, a <c>.lrc</c>); a
    /// <see cref="Companion"/> is a non-metadata resource of the work (cover art, bundled tools).
    /// Only primaries take part in library browsing; sidecars/companions are folded into their primary.
    /// </summary>
    public enum FileRole
    {
        Primary = 0,
        Sidecar = 1,
        Companion = 2,
    }
}
