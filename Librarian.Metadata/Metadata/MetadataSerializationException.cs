using Librarian.Util;
using System.Xml.Linq;

namespace Librarian.Metadata
{
    public class MetadataSerializationException : Exception
    {
        public XObject Object { get; set; }

        public string FormattedMessage
        {
            get
            {
                string prefix = "";
                var lineInfo = Object.LineInfo();
                if (lineInfo.HasValue)
                    prefix = $"At {lineInfo.Value.line}:{lineInfo.Value.position}";

                return prefix + Message;
            }
        }

        public MetadataSerializationException(XObject @object, string? message) : base(message)
        {
            Object = @object;
        }

        public MetadataSerializationException(XObject @object, string? message, Exception? innerException) : base(message, innerException)
        {
            Object = @object;
        }
    }
}
