namespace Librarian.Model
{
    /// <summary>
    /// Where an <see cref="IndexedFile"/>'s bytes physically live (collection_plan.md §3.1). A
    /// <see cref="Filesystem"/> file is a real file on disk; an <see cref="ArchiveEntry"/> is a virtual
    /// file inside an archive, addressed by (<see cref="IndexedFile.ParentFileId"/>,
    /// <see cref="IndexedFile.InternalPath"/>) rather than by an on-disk path. Disk I/O must check this
    /// to decide whether to open the path directly or route through the archive byte-reader.
    /// </summary>
    public enum FileSource
    {
        Filesystem = 0,
        ArchiveEntry = 1,
    }
}
