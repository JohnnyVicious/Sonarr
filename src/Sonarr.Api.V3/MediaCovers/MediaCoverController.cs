#pragma warning disable SA1005
using System; //NOSONAR S3990: This legacy assembly needs an API-wide CLS migration.
#pragma warning restore SA1005
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using Sonarr.Http;

namespace Sonarr.Api.V3.MediaCovers
{
    [V3ApiController]
    public class MediaCoverController : Controller
    {
        private static readonly Regex RegexResizedImage = new Regex(@"-\d+\.jpg$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IContentTypeProvider _mimeTypeProvider;

        public MediaCoverController(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _mimeTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet(@"{seriesId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetMediaCover(int seriesId, string filename)
        {
            var requestedFilename = filename ?? string.Empty;

            if (string.IsNullOrWhiteSpace(requestedFilename))
            {
                return NotFound();
            }

            // nosemgrep: codacy.csharp.security.null-dereference -- requestedFilename is normalized and checked above.
            var filePath = GetMediaCoverPath(seriesId, requestedFilename);

            if (filePath == null)
            {
                return NotFound();
            }

            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, ".jpg");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return NotFound();
                }

                filePath = basefilePath;
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }

        private string GetMediaCoverPath(int seriesId, string filename)
        {
            if (filename == null)
            {
                return null;
            }

            var appDataPath = _appFolderInfo.GetAppDataPath();
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                return null;
            }

            try
            {
                return GetMediaCoverPathInsideFolder(appDataPath, seriesId, filename);
            }
            catch (Exception ex) when (IsPathResolutionException(ex))
            {
                return null;
            }
        }

        private static string GetMediaCoverPathInsideFolder(string appDataPath, int seriesId, string filename)
        {
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                throw new ArgumentNullException(nameof(appDataPath));
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }

            // nosemgrep: codacy.csharp.security.null-dereference -- appDataPath and filename are checked before combining.
            var folderPath = Path.Combine(appDataPath, "MediaCover", seriesId.ToString());
            var fullFolderPath = Path.GetFullPath(folderPath);
            var fullFilePath = Path.GetFullPath(Path.Combine(fullFolderPath, filename));

            return IsPathInsideFolder(fullFilePath, fullFolderPath) ? fullFilePath : null;
        }

        private static bool IsPathInsideFolder(string fullFilePath, string folderPath)
        {
            if (fullFilePath == null)
            {
                throw new ArgumentNullException(nameof(fullFilePath));
            }

            if (folderPath == null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            var folderPathWithSeparator = EnsureTrailingDirectorySeparator(folderPath);
            return fullFilePath.StartsWith(folderPathWithSeparator, DiskProviderBase.PathStringComparison);
        }

        private static bool IsPathResolutionException(Exception ex)
        {
            return ex is ArgumentException ||
                   ex is ArgumentNullException ||
                   ex is NotSupportedException ||
                   ex is PathTooLongException ||
                   ex is IOException ||
                   ex is UnauthorizedAccessException;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private string GetContentType(string filePath)
        {
            if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
