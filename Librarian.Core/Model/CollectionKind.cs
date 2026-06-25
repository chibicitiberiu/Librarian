namespace Librarian.Model
{
    /// <summary>
    /// The structural kind of a <see cref="Collection"/> (collection_plan.md §3.2). Chosen by
    /// <c>inferKind</c> from folder/archive structure and naming; <see cref="RoleSource.Manual"/>
    /// pins it across re-association runs.
    /// </summary>
    public enum CollectionKind
    {
        Generic = 0,
        Show = 1,
        Season = 2,
        Album = 3,
        Series = 4, // books
        AppBundle = 5,
    }
}
