namespace Librarian.ViewModels
{
    /// <summary>One segment of the shared location/breadcrumb bar rendered by <c>_Layout</c>. A page sets
    /// <c>ViewData["Breadcrumbs"]</c> to an ordered list of these so every page gets the same toolbar
    /// instead of hand-rolling its own. <see cref="Url"/> null renders the segment as plain (current
    /// location); <see cref="IconUrl"/> shows a leading icon (used for the root segment).</summary>
    public record Breadcrumb(string Name, string? Url = null, string? IconUrl = null);
}
