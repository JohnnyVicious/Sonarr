using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;

namespace Sonarr.Http.Frontend.Mappers
{
    public abstract class StaticResourceMapperBase : IMapHttpRequestsToDisk
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;
        private readonly StringComparison _caseSensitive;
        private readonly IContentTypeProvider _mimeTypeProvider;

        protected StaticResourceMapperBase(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;

            _mimeTypeProvider = new FileExtensionContentTypeProvider();
            _caseSensitive = RuntimeInfo.IsProduction ? DiskProviderBase.PathStringComparison : StringComparison.OrdinalIgnoreCase;
        }

        protected abstract string FolderPath { get; }
        protected abstract string MapPath(string resourceUrl);

        public abstract bool CanHandle(string resourceUrl);

        public string Map(string resourceUrl)
        {
            return GetMappedPathInsideFolder(MapPath(resourceUrl));
        }

        public Task<IActionResult> GetResponse(HttpContext context, string resourceUrl)
        {
            var filePath = Map(resourceUrl);

            if (filePath == null)
            {
                return Task.FromResult<IActionResult>(null);
            }

            if (_diskProvider.FileExists(filePath, _caseSensitive))
            {
                if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                return Task.FromResult<IActionResult>(new FileStreamResult(GetContentStream(context, filePath), new MediaTypeHeaderValue(contentType)
                {
                    Encoding = contentType == "text/plain" ? Encoding.UTF8 : null
                }));
            }

            _logger.Warn("File {0} not found", filePath);

            return Task.FromResult<IActionResult>(null);
        }

        protected virtual Stream GetContentStream(HttpContext context, string filePath)
        {
            return File.OpenRead(filePath);
        }

        protected bool IsPathInsideFolder(string filePath)
        {
            return GetMappedPathInsideFolder(filePath) != null;
        }

        private string GetMappedPathInsideFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var folderPath = FolderPath;
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            try
            {
                var fullFilePath = Path.GetFullPath(filePath);
                var fullFolderPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(folderPath));

                return fullFilePath.StartsWith(fullFolderPath, _caseSensitive) ? fullFilePath : null;
            }
            catch (Exception ex) when (IsPathResolutionException(ex))
            {
                return null;
            }
        }

        private static bool IsPathResolutionException(Exception ex)
        {
            return ex is ArgumentException ||
                   ex is NotSupportedException ||
                   ex is PathTooLongException ||
                   ex is IOException ||
                   ex is UnauthorizedAccessException;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
