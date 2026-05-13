using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Common
{
    public interface IArchiveService
    {
        void Extract(string compressedFile, string destination);
        void CreateZip(string path, IEnumerable<string> files);
    }

    public class ArchiveService : IArchiveService
    {
        private readonly Logger _logger;

        public ArchiveService(Logger logger)
        {
            _logger = logger;
        }

        public void Extract(string compressedFile, string destination)
        {
            if (compressedFile == null)
            {
                throw new ArgumentNullException(nameof(compressedFile));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            _logger.Debug("Extracting archive [{0}] to [{1}]", compressedFile, destination);

            if (compressedFile.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                ExtractZip(compressedFile, destination);
            }
            else
            {
                ExtractTgz(compressedFile, destination);
            }

            _logger.Debug("Extraction complete.");
        }

        public void CreateZip(string path, IEnumerable<string> files)
        {
            _logger.Debug("Creating archive {0}", path);

            using var zipFile = ZipFile.Create(path);

            zipFile.BeginUpdate();

            foreach (var file in files)
            {
                zipFile.Add(file, Path.GetFileName(file));
            }

            zipFile.CommitUpdate();
        }

        private void ExtractZip(string compressedFile, string destination)
        {
            if (compressedFile == null)
            {
                throw new ArgumentNullException(nameof(compressedFile));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            Directory.CreateDirectory(destination);

            using (var fileStream = File.OpenRead(compressedFile))
            {
                var zipFile = new ZipFile(fileStream);

                _logger.Debug("Validating Archive {0}", compressedFile);

                if (!zipFile.TestArchive(true, TestStrategy.FindFirstError, OnZipError))
                {
                    throw new IOException(string.Format("File {0} failed archive validation.", compressedFile));
                }

                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (zipEntry == null)
                    {
                        continue;
                    }

                    var entryFileName = zipEntry.Name;
                    var fullZipToPath = GetSafeExtractionPath(destination, entryFileName);

                    if (IsZipSymlink(zipEntry))
                    {
                        throw new IOException($"Archive entry '{entryFileName}' is not a regular file.");
                    }

                    if (!zipEntry.IsFile)
                    {
                        continue; // Ignore directories
                    }

                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    var buffer = new byte[4096]; // 4K is optimum
                    var zipStream = zipFile.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    var directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (var streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
        }

        private void ExtractTgz(string compressedFile, string destination)
        {
            if (compressedFile == null)
            {
                throw new ArgumentNullException(nameof(compressedFile));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            Directory.CreateDirectory(destination);

            using (Stream inStream = File.OpenRead(compressedFile))
            using (Stream gzipStream = new GZipInputStream(inStream))
            using (var tarStream = new TarInputStream(gzipStream, Encoding.UTF8))
            {
                TarEntry tarEntry;
                while ((tarEntry = tarStream.GetNextEntry()) != null)
                {
                    var fullPath = GetSafeExtractionPath(destination, tarEntry.Name);

                    if (tarEntry.IsDirectory)
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    if (!IsRegularTarEntry(tarEntry))
                    {
                        throw new IOException($"Archive entry '{tarEntry.Name}' is not a regular file.");
                    }

                    var directoryName = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    using (var streamWriter = File.Create(fullPath))
                    {
                        tarStream.CopyEntryContents(streamWriter);
                    }
                }
            }
        }

        private static string GetSafeExtractionPath(string destination, string entryName)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (entryName == null)
            {
                throw new IOException("Archive entry has an invalid file name.");
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new IOException("Archive entry has an invalid file name.");
            }

            var pathSegments = GetRelativePathSegments(entryName);
            var destinationPath = Path.GetFullPath(destination);
            var combinedPath = Path.Combine(new[] { destinationPath }.Concat(pathSegments).ToArray());
            var fullPath = Path.GetFullPath(combinedPath);

            if (!IsPathInsideFolder(fullPath, destinationPath))
            {
                ThrowUnsafeArchiveEntry(entryName);
            }

            return fullPath;
        }

        private static string[] GetRelativePathSegments(string entryName)
        {
            if (entryName == null)
            {
                throw new ArgumentNullException(nameof(entryName));
            }

            var normalizedEntryName = entryName.Replace('\\', '/');

            if (IsRootedArchiveEntry(entryName, normalizedEntryName))
            {
                ThrowUnsafeArchiveEntry(entryName);
            }

            var pathSegments = normalizedEntryName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0 || pathSegments.Any(s => s == "." || s == ".."))
            {
                ThrowUnsafeArchiveEntry(entryName);
            }

            return pathSegments;
        }

        private static bool IsRootedArchiveEntry(string entryName, string normalizedEntryName)
        {
            if (entryName == null)
            {
                throw new ArgumentNullException(nameof(entryName));
            }

            if (normalizedEntryName == null)
            {
                throw new ArgumentNullException(nameof(normalizedEntryName));
            }

            return Path.IsPathRooted(entryName) ||
                   normalizedEntryName.StartsWith("/", StringComparison.Ordinal) ||
                   normalizedEntryName.Contains(':');
        }

        private static bool IsPathInsideFolder(string fullPath, string folderPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            if (folderPath == null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            var folderWithSeparator = EnsureTrailingDirectorySeparator(folderPath);
            return fullPath.StartsWith(folderWithSeparator, DiskProviderBase.PathStringComparison);
        }

        private static void ThrowUnsafeArchiveEntry(string entryName)
        {
            throw new IOException($"Archive entry '{entryName}' targets a path outside the extraction folder.");
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

        private static bool IsZipSymlink(ZipEntry zipEntry)
        {
            if (zipEntry == null)
            {
                throw new ArgumentNullException(nameof(zipEntry));
            }

            const int unixHostSystem = 3;
            const int unixFileTypeMask = 0xF000;
            const int unixSymlinkFileType = 0xA000;

            if (zipEntry.HostSystem != unixHostSystem)
            {
                return false;
            }

            var unixMode = (zipEntry.ExternalFileAttributes >> 16) & unixFileTypeMask;

            return unixMode == unixSymlinkFileType;
        }

        private static bool IsRegularTarEntry(TarEntry tarEntry)
        {
            if (tarEntry == null)
            {
                return false;
            }

            var tarHeader = tarEntry.TarHeader;
            if (tarHeader == null)
            {
                return false;
            }

            var typeFlag = tarHeader.TypeFlag;

            return typeFlag == TarHeader.LF_OLDNORM ||
                   typeFlag == TarHeader.LF_NORMAL;
        }

        private void OnZipError(TestStatus status, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _logger.Error("File {0} failed zip validation. {1}", status.File.Name, message);
            }
        }
    }
}
