using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Providers.MetadataCli
{
    public class MetadataCliProvider : IMetadataProvider
    {
        private static readonly Guid providerId = new("9bf2d3bf-d645-4ce3-8c84-37dc92f68c3b");
        private readonly MetadataCliService cliService;
        private readonly MetadataFactory metadataFactory;
        private readonly ILogger logger;

        public MetadataCliProvider(MetadataCliService cliService,
                                   MetadataFactory metadataFactory,
                                   ILogger<MetadataCliProvider> logger)
        {
            this.cliService = cliService;
            this.metadataFactory = metadataFactory;
            this.logger = logger;
        }

        public Guid ProviderId => providerId;

        public string DisplayName => nameof(MetadataCliProvider);

        public async Task<MetadataCollection> GetMetadataAsync(string filePath)
        {
            MetadataCollection result = new();
            var metadata = await cliService.GetMetadataAsync(filePath) ?? throw new Exception("No metadata returned!");

            if (metadata.Parser != "avformat")
                logger.LogWarning("Parser {parser} may have different attributes than expected!", metadata.Parser);

            if (metadata.Streams != null)
                ProcessStreams(result, metadata.Streams);

            if (metadata.Chapters != null)
                ProcessChapters(result, metadata.Chapters);

            CollectFileMetadata(result, metadata);

            return result;
        }

        private void ProcessStreams(MetadataCollection result, IEnumerable<MetadataCliStream> streams)
        {
            foreach (var stream in streams)
            {
                SubResource streamResource = new()
                {
                    Kind = SubResourceKind.Stream,
                    Name = stream.Type != null ? $"{stream.Type} stream {stream.Id}" : $"Stream {stream.Id}",
                    InternalId = stream.Id
                };
                result.AddSubResource(streamResource);
                CollectStreamMetadata(result, stream, streamResource);
            }
        }

        private void ProcessChapters(MetadataCollection result, IEnumerable<MetadataCliChapter> chapters)
        {
            foreach (MetadataCliChapter chapter in chapters)
            {
                SubResource streamResource = new()
                {
                    Kind = SubResourceKind.Stream,
                    Name = $"Chapter {chapter.Id}"
                };
                result.AddSubResource(streamResource);
                CollectChapterMetadata(result, chapter, streamResource);

                if (chapter.Title != null)
                    streamResource.Name = chapter.Title;
            }
        }

        private void CollectFileMetadata(MetadataCollection result, MetadataCliResult metadata)
        {
            if (metadata.Metadata != null)
            {
                foreach (var pair in metadata.Metadata)
                {
                    if (MetadataFactory.IsStreamLanguageAttribute(pair.Key))
                    {
                        int streamId = MetadataFactory.StreamLanguageGetStreamId(pair.Key);
                        var stream = result.SubResources.FirstOrDefault(res => res.Kind == SubResourceKind.Stream && res.InternalId == streamId);
                        if (stream != null)
                            result.Add(metadataFactory.Create(General.Language, pair.Value, ProviderId, false, stream));
                    }
                    else
                    {
                        var metadataBase = metadataFactory.Create(pair.Key, pair.Value, ProviderId, true);
                        if (metadataBase == null)
                            continue;

                        result.Add(metadataBase);
                    }
                }
            }

            // general media attributes
            if (metadata.BitRate != null && metadata.BitRate != 0L)
            {
                result.Add(metadataFactory.Create(Media.BitRate, metadata.BitRate, ProviderId, false));
            }
            else if (metadata.Streams != null)
            {
                long? bitRate = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Video or MetadataCliStreamType.Audio)
                    .Where(stream => stream.BitRate != null)
                    .Sum(stream => stream.BitRate);
                if (bitRate != null && bitRate != 0L)
                    result.Add(metadataFactory.Create(Media.BitRate, bitRate, ProviderId, false));
            }

            if (metadata.Streams != null)
            {
                double? duration = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Video or MetadataCliStreamType.Audio)
                    .Where(stream => stream.Duration != null)
                    .Max(stream => stream.Duration);
                if (duration != null)
                    result.Add(metadataFactory.Create(Media.Duration, duration, ProviderId, false));

                // image attributes
                var bestStream = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Video)
                    .Where(stream => stream.Width != null && stream.Height != null)
                    .OrderByDescending(stream => stream.Width * stream.Height)
                    .FirstOrDefault();

                if (bestStream != null)
                {
                    result.Add(metadataFactory.Create(Image.Width, bestStream.Width!, ProviderId, false));
                    result.Add(metadataFactory.Create(Image.Height, bestStream.Height!, ProviderId, false));

                    if (bestStream.AspectRatio != null)
                        result.Add(metadataFactory.Create(Image.AspectRatio, bestStream.AspectRatio, ProviderId, false));
                    else
                        result.Add(metadataFactory.Create(Image.AspectRatio, Convert.ToDouble(bestStream.Width) / Convert.ToDouble(bestStream.Height), ProviderId, false));

                    result.Add(metadataFactory.Create(Image.Pixels, bestStream.Width! * bestStream.Height!, ProviderId, false));
                }

                // video attributes
                double? framerate = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Video)
                    .Where(stream => stream.FrameRate != null || stream.RealFrameRate != null)
                    .Select(stream => stream.RealFrameRate ?? stream.FrameRate)
                    .Max();

                if (framerate != null)
                    result.Add(metadataFactory.Create(Video.FrameRate, framerate, ProviderId, false));

                long? frames = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Video)
                    .Where(stream => stream.Frames != null)
                    .Max(stream => stream.Frames);

                if (frames != null)
                    result.Add(metadataFactory.Create(Video.Frames, frames, ProviderId, false));

                // audio attributes
                long? bitsPerSample = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Audio)
                    .Where(stream => stream.BitsPerSample != null)
                    .Max(stream => stream.BitsPerSample);

                if (bitsPerSample != null)
                    result.Add(metadataFactory.Create(Audio.BitsPerSample, bitsPerSample, ProviderId, false));

                long? channels = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Audio)
                    .Where(stream => stream.Channels != null)
                    .Max(stream => stream.Channels);

                if (channels != null)
                    result.Add(metadataFactory.Create(Audio.Channels, channels, ProviderId, false));

                long? sampleRate = metadata.Streams
                    .Where(stream => stream.Type is MetadataCliStreamType.Audio)
                    .Where(stream => stream.SampleRate != null)
                    .Max(stream => stream.SampleRate);

                if (sampleRate != null)
                    result.Add(metadataFactory.Create(Audio.SampleRate, sampleRate, ProviderId, false));
            }
        }

        private void CollectStreamMetadata(MetadataCollection result, MetadataCliStream stream, SubResource streamResource)
        {
            if (stream.Metadata != null)
            {
                foreach (var pair in stream.Metadata)
                {
                    var metadataBase = metadataFactory.Create(pair.Key, pair.Value, ProviderId, true, streamResource);
                    if (metadataBase == null)
                        continue;

                    if (metadataBase.AttributeDefinition == General.Title)
                        stream.Title = pair.Value.ToString()!.Trim();
                    else if (metadataBase.AttributeDefinition == Media.BitRate)
                        stream.BitRate ??= Convert.ToInt64(pair.Value);
                    else if (metadataBase.AttributeDefinition == Media.Duration)
                        stream.Duration ??= TimeSpan.Parse(pair.Value.ToString()!).TotalSeconds;
                    else if (metadataBase.AttributeDefinition == Video.Frames)
                        stream.Frames ??= Convert.ToInt64(pair.Value);
                    else result.Add(metadataBase);
                }
            }

            // general/identification
            result.Add(metadataFactory.Create(General.Id, stream.Id, ProviderId, false, streamResource));
            if (stream.Index != null)
                result.Add(metadataFactory.Create(General.Index, stream.Index, ProviderId, false, streamResource));
            if (stream.Type != null)
                result.Add(metadataFactory.Create(Media.StreamType, stream.Type, ProviderId, false, streamResource));

            // general media attributes
            if (stream.BitRate != null)
                result.Add(metadataFactory.Create(Media.BitRate, stream.BitRate, ProviderId, false, streamResource));
            if (!string.IsNullOrWhiteSpace(stream.Codec))
                result.Add(metadataFactory.Create(Media.Codec, stream.Codec.Trim(), ProviderId, false, streamResource));
            if (stream.Duration != null)
                result.Add(metadataFactory.Create(Media.Duration, stream.Duration, ProviderId, false, streamResource));
            if (stream.StartTime != null)
                result.Add(metadataFactory.Create(Media.StartTime, stream.StartTime, ProviderId, false, streamResource));

            // image attributes
            if (stream.AspectRatio != null)
                result.Add(metadataFactory.Create(Image.AspectRatio, stream.AspectRatio, ProviderId, false, streamResource));
            else if (stream.Width != null && stream.Height != null)
                result.Add(metadataFactory.Create(Image.AspectRatio, Convert.ToDouble(stream.Width) / Convert.ToDouble(stream.Height), ProviderId, false, streamResource));

            if (stream.Width != null)
                result.Add(metadataFactory.Create(Image.Width, stream.Width, ProviderId, false, streamResource));
            if (stream.Height != null)
                result.Add(metadataFactory.Create(Image.Height, stream.Height, ProviderId, false, streamResource));
            if (stream.Width != null && stream.Height != null)
                result.Add(metadataFactory.Create(Image.Pixels, stream.Width * stream.Height, ProviderId, false, streamResource));

            // video attributes
            if (stream.RealFrameRate != null)
                result.Add(metadataFactory.Create(Video.FrameRate, stream.RealFrameRate, ProviderId, false, streamResource));
            else if (stream.FrameRate != null)
                result.Add(metadataFactory.Create(Video.FrameRate, stream.FrameRate, ProviderId, false, streamResource));
            else if (stream.Frames != null)
                result.Add(metadataFactory.Create(Video.Frames, stream.Frames, ProviderId, false, streamResource));

            // audio attributes
            if (stream.BitsPerSample != null)
                result.Add(metadataFactory.Create(Audio.BitsPerSample, stream.BitsPerSample, ProviderId, false, streamResource));
            if (stream.Channels != null)
                result.Add(metadataFactory.Create(Audio.Channels, stream.Channels, ProviderId, false, streamResource));
            if (stream.SampleRate != null)
                result.Add(metadataFactory.Create(Audio.SampleRate, stream.SampleRate, ProviderId, false, streamResource));
        }

        private void CollectChapterMetadata(MetadataCollection result, MetadataCliChapter chapter, SubResource chapterResource)
        {
            if (chapter.Metadata != null)
            {
                foreach (var pair in chapter.Metadata)
                {
                    var metadataBase = metadataFactory.Create(pair.Key, pair.Value, ProviderId, true, chapterResource);
                    if (metadataBase == null)
                        continue;

                    if (metadataBase.AttributeDefinition == General.Title)
                        chapter.Title = pair.Value.ToString()!.Trim();
                    else result.Add(metadataBase);
                }
            }

            result.Add(metadataFactory.Create(General.Id, chapter.Id, ProviderId, false, chapterResource));
            if (chapter.Start != null)
                result.Add(metadataFactory.Create(Media.StartTime, chapter.Start, ProviderId, false, chapterResource));
            if (chapter.End != null)
                result.Add(metadataFactory.Create(Media.EndTime, chapter.End, ProviderId, false, chapterResource));
            if (chapter.Start != null && chapter.End != null)
                result.Add(metadataFactory.Create(Media.Duration, chapter.End - chapter.Start, ProviderId, false, chapterResource));
        }

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
