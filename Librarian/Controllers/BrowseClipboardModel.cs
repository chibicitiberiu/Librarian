namespace Librarian.Controllers
{
    public class BrowseClipboardModel
    {
        public bool Move { get; set; }
        public string[] SourceFiles { get; set; } = null!;
    }
}
