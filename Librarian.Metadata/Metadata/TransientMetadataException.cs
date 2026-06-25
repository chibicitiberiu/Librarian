namespace Librarian.Metadata
{
    /// <summary>
    /// A transient, retryable failure from a metadata provider — a network blip, a server 5xx, or a
    /// timeout, as opposed to a permanent condition (unsupported file, parse error). The provider
    /// execution policy retries these with backoff and, if they persist, marks the file's extraction
    /// incomplete; non-transient errors are not retried and do not flag the file.
    /// </summary>
    public class TransientMetadataException : Exception
    {
        public TransientMetadataException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
