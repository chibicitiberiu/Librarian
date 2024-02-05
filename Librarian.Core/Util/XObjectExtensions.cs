using System.Xml;
using System.Xml.Linq;

namespace Librarian.Util
{
    public static class XObjectExtensions
    {
        public static string? Text(this XElement element)
        {
            return element.Nodes().OfType<XText>().FirstOrDefault()?.Value;
        }

        public static (int line, int position)? LineInfo(this XObject @object)
        {
            if (@object is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                return (lineInfo.LineNumber, lineInfo.LinePosition);

            return null;
        }
    }
}
