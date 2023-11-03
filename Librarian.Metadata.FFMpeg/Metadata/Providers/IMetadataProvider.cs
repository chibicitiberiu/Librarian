using FFMpegCore;
using System.Collections;
using System.Collections.Generic;

namespace Librarian.Metadata.Providers
{
    public class FFMpegMetadataProvider : IMetadataProvider
    {
        public int ProviderId => 21635487;

        public string DisplayName => "FFMpeg";

        public IEnumerable<MetadataField> GetMetadata(string filePath)
        {
            var probe = FFProbe.Analyse(filePath);
            probe.Format.
        }

        public void SaveMetadata(string filePath, IEnumerable<MetadataField> metadata)
        {
            throw new NotImplementedException();
        }
    }
}
