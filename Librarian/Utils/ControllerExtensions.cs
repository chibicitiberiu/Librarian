using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MimeMapping;
using System.IO;

namespace Librarian.Utils
{
    public static class ControllerExtensions
    {
        public static FileStreamResult InlineFileFromDisk(this ControllerBase @this, string fullPath)
        {
            var fileInfo = new FileInfo(fullPath);

            ContentDispositionHeaderValue cd = new("inline")
            {
                FileName = fileInfo.Name,
                CreationDate = fileInfo.CreationTimeUtc,
                ModificationDate = fileInfo.LastAccessTimeUtc,
                ReadDate = fileInfo.LastAccessTimeUtc,
                Size = fileInfo.Length
            };

            @this.Response.Headers.Add(HeaderNames.ContentDisposition, cd.ToString());

            return @this.File(File.OpenRead(fullPath),
                contentType: MimeUtility.GetMimeMapping(fullPath),
                fileDownloadName: null,
                enableRangeProcessing: false);
        }

        public static FileStreamResult FileFromDisk(this ControllerBase @this, string fullPath)
        {
            var fileInfo = new FileInfo(fullPath);

            ContentDispositionHeaderValue cd = new("attachment")
            {
                FileName = fileInfo.Name,
                CreationDate = fileInfo.CreationTimeUtc,
                ModificationDate = fileInfo.LastAccessTimeUtc,
                ReadDate = fileInfo.LastAccessTimeUtc,
                Size = fileInfo.Length
            };

            @this.Response.Headers.Add(HeaderNames.ContentDisposition, cd.ToString());

            return @this.File(File.OpenRead(fullPath),
                contentType: MimeUtility.GetMimeMapping(fullPath),
                fileDownloadName: fileInfo.Name,
                enableRangeProcessing: false);
        }
    }
}
