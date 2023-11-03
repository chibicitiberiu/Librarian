namespace Librarian.ViewModels
{
    public class BrowseCopyRequest
    {
        public string Path { get; set; } = null!;
        public string[] Items { get; set; } = null!;
    }
}
