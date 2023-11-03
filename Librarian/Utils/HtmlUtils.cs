namespace Librarian.Utils
{
    public static class HtmlUtils
    {
        public static string ActiveOnPage(string page, object? currentPage, string activeClass = "active")
        {
            if (Equals(page, currentPage))
            {
                return activeClass;
            }
            return "";
        }

        public static string BoolToClass(bool value, string cssClass)
        {
            return value ? cssClass : string.Empty;
        }
    }
}