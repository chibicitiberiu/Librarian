using Librarian.DB;
using Librarian.Model;
using Librarian.Metadata;
using Librarian.Services;
using Librarian.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Indexing
{
    public class MetadataUpdateJob : IJob
    {
        public static readonly JobKey Key = new(nameof(MetadataUpdateJob));
        private readonly ILogger<MetadataUpdateJob> logger;
        private readonly DatabaseContext dbContext;
        private readonly MetadataService metadataService;
        private readonly IConfiguration config;

        public MetadataUpdateJob(ILogger<MetadataUpdateJob> logger,
                                 DatabaseContext dbContext,
                                 MetadataService metadataService,
                                 IConfiguration config)
        {
            this.logger = logger;
            this.dbContext = dbContext;
            this.metadataService = metadataService;
            this.config = config;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var fileId = context.MergedJobDataMap.GetInt("fileId");
            var indexedFile = await dbContext.IndexedFiles.FindAsync(fileId);
            if (indexedFile == null)
            {
                logger.LogError("Invalid file ID {}", fileId);
                return;
            }

            logger.LogInformation("Getting metadata for {0}...", indexedFile.Path);

            var metadata = await metadataService.GetMetadataAsync(indexedFile.Path);
            foreach (var field in metadata)
            {
                MetadataAttributeDefinition attribute = field.AttributeDefinition;

                // Get or create metadata
                switch (attribute.Type)
                {
                    case MetadataType.Text:
                    case MetadataType.BigText:
                    case MetadataType.FormattedText:
                        SaveTextMetadata(fileId, indexedFile, (TextMetadata)field, attribute);
                        break;

                    case MetadataType.Integer:
                        SaveIntMetadata(fileId, indexedFile, (IntegerMetadata)field, attribute);
                        break;

                    case MetadataType.Float:
                        SaveFloatMetadata(fileId, indexedFile, (FloatMetadata)field, attribute);
                        break;

                    case MetadataType.Date:
                        SaveDateMetadata(fileId, indexedFile, (DateMetadata)field, attribute);
                        break;

                    case MetadataType.Blob:
                        SaveBlobMetadata(fileId, indexedFile, (BlobMetadata)field, attribute);
                        break;
                }
            }
            await dbContext.SaveChangesAsync();
        }

        private void SaveBlobMetadata(int fileId, IndexedFile indexedFile, BlobMetadata field, MetadataAttributeDefinition attribute)
        {
            var blobMetadata = dbContext.BlobMetadata.FirstOrDefault(x => x.FileId == fileId && x.AttributeDefinition.Id == attribute.Id);
            if (blobMetadata == null)
            {
                blobMetadata = new BlobMetadata();
                dbContext.Add(blobMetadata);
            }
            blobMetadata.AttributeDefinition = attribute;
            blobMetadata.Value = (byte[])field.Value;
            blobMetadata.File = indexedFile;
            blobMetadata.ProviderId = field.ProviderId;
        }

        private void SaveDateMetadata(int fileId, IndexedFile indexedFile, DateMetadata field, MetadataAttributeDefinition attribute)
        {
            var dateMetadata = dbContext.DateMetadata.FirstOrDefault(x => x.FileId == fileId && x.AttributeDefinition.Id == attribute.Id);
            if (dateMetadata == null)
            {
                dateMetadata = new DateMetadata();
                dbContext.Add(dateMetadata);
            }
            dateMetadata.AttributeDefinition = attribute;
            //if (field.Value is DateTime dt)
            //    dateMetadata.Value = new DateTimeOffset(dt).ToUniversalTime();
            //else dateMetadata.Value = ((DateTimeOffset)field.Value).ToUniversalTime();
            dateMetadata.File = indexedFile;
            dateMetadata.ProviderId = field.ProviderId;
        }

        private void SaveFloatMetadata(int fileId, IndexedFile indexedFile, FloatMetadata field, MetadataAttributeDefinition attribute)
        {
            var floatMetadata = dbContext.FloatMetadata.FirstOrDefault(x => x.FileId == fileId && x.AttributeDefinition.Id == attribute.Id);
            if (floatMetadata == null)
            {
                floatMetadata = new FloatMetadata();
                dbContext.Add(floatMetadata);
            }
            floatMetadata.AttributeDefinition = attribute;
            //if (field.Value is TimeSpan ts)
            //    floatMetadata.Value = ts.TotalSeconds;
            //else
                floatMetadata.Value = Convert.ToDouble(field.Value);
            floatMetadata.File = indexedFile;
            floatMetadata.ProviderId = field.ProviderId;
        }

        private void SaveIntMetadata(int fileId, IndexedFile indexedFile, IntegerMetadata field, MetadataAttributeDefinition attribute)
        {
            var intMetadata = dbContext.IntegerMetadata.FirstOrDefault(x => x.FileId == fileId && x.AttributeDefinition.Id == attribute.Id);
            if (intMetadata == null)
            {
                intMetadata = new IntegerMetadata();
                dbContext.Add(intMetadata);
            }
            intMetadata.AttributeDefinition = attribute;
            intMetadata.Value = Convert.ToInt64(field.Value);
            intMetadata.File = indexedFile;
            intMetadata.ProviderId = field.ProviderId;
        }

        private void SaveTextMetadata(int fileId, IndexedFile indexedFile, TextMetadata field, MetadataAttributeDefinition attribute)
        {
            var textMetadata = dbContext.TextMetadata.FirstOrDefault(x => x.FileId == fileId && x.AttributeDefinition.Id == attribute.Id);
            if (textMetadata == null)
            {
                textMetadata = new TextMetadata();
                dbContext.Add(textMetadata);
            }
            textMetadata.AttributeDefinition = attribute;
            textMetadata.Value = (string)field.Value;
            //textMetadata.ValueSearch = LanguageHelper.CreateTsVector(textMetadata.Value, config);
            textMetadata.File = indexedFile;
            textMetadata.ProviderId = field.ProviderId;
        }

        //private async Task<MetadataAttributeDefinition> GetOrCreateMetadataAttribute(MetadataBase field)
        //{
        //    var attribute = dbContext.MetadataAttributes.FirstOrDefault(
        //        x => x.Name == field.Definition.Name && x.Group == field.Definition.Group);

        //    // try creating the object
        //    if (attribute == null)
        //    {
        //        attribute = new MetadataAttributeDefinition()
        //        {
        //            Name = field.Definition.Name,
        //            Group = field.Definition.Group,
        //            Type = field.Definition.Type,
        //        };
        //        dbContext.MetadataAttributes.Add(attribute);
        //        try
        //        {
        //            await dbContext.SaveChangesAsync();
        //        }
        //        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pex && pex.SqlState == PostgresErrorCodes.UniqueViolation)
        //        {
        //            // attribute was created in parallel
        //            attribute = dbContext.MetadataAttributes.First(
        //                x => x.Name == field.Definition.Name && x.Group == field.Definition.Group);
        //        }
        //    }

        //    return attribute;
        //}
    }
}
