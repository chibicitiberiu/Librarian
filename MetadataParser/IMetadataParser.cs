using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetadataParser
{
    public interface IMetadataParser
    {
        public IEnumerable<KeyValuePair<string, object>> ParseMetadata(Stream inputStream);
    }
}
