namespace Librarian.ViewModels
{
    public class BrowseRenameRequest
    {
        public string Path { get; set; } = null!;
        public string Item { get; set; } = null!;
        public string NewName { get; set; } = null!;
    }
}
