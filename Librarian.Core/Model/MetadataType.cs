namespace Librarian.Model
{
    public enum MetadataType
    {
        // stored in TextMetadata
        Text,

        // stored in TextMetadata
        BigText,

        // stored in TextMetadata
        FormattedText,

        // stored in IntegerMetadata
        Integer,

        // stored in FloatMetadata
        Float,

        // stored in DateTimeMetadata
        Date,

        // stored in FloatMetadata (as number of seconds)
        TimeSpan,

        // stored in BlobMetadata
        Blob
    }
}
