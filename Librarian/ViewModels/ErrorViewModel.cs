namespace Librarian.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>The HTTP status code that triggered the error page (404, 403, 500, …).</summary>
        public int StatusCode { get; set; } = 500;

        public string Title => StatusCode switch
        {
            400 => "Bad request",
            403 => "Access denied",
            404 => "Not found",
            500 => "Something went wrong",
            _ => $"Error {StatusCode}",
        };

        public string Description => StatusCode switch
        {
            400 => "The request couldn't be understood.",
            403 => "You don't have permission to view this.",
            404 => "The page or file you're looking for doesn't exist or has moved.",
            500 => "An unexpected error occurred while processing your request.",
            _ => "An error occurred while processing your request.",
        };
    }
}
