namespace Librarian.ViewModels
{
    public class BrowseDeleteRequest
    {
        public string Path { get; set; } = null!;
        public string[] Items { get; set; } = null!;
    }
}
