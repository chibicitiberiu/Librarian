using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Librarian.Model
{
    public enum SubResourceKind
    {
        [XmlEnum("unknown")]
        Unknown = 0,
        [XmlEnum("stream")]
        Stream = 1,
        [XmlEnum("chapter")]
        Chapter = 2,
    }
}
