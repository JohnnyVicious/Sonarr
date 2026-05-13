using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using NzbDrone.Common.Disk;
using NLog;

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
                    if (directoryName.Length > 0)
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
                    if (directoryName.Length > 0)
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
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new IOException("Archive entry has an invalid file name.");
            }

            var normalizedEntryName = entryName.Replace('\\', '/');

            if (Path.IsPathRooted(entryName) ||
                normalizedEntryName.StartsWith("/", StringComparison.Ordinal) ||
                normalizedEntryName.Contains(":", StringComparison.Ordinal))
            {
                throw new IOException($"Archive entry '{entryName}' targets a path outside the extraction folder.");
            }

            var pathSegments = normalizedEntryName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (!pathSegments.Any() || pathSegments.Any(s => s == "." || s == ".."))
            {
                throw new IOException($"Archive entry '{entryName}' targets a path outside the extraction folder.");
            }

            var destinationPath = Path.GetFullPath(destination);
            var destinationWithSeparator = EnsureTrailingDirectorySeparator(destinationPath);
            var combinedPath = Path.Combine(new[] { destinationPath }.Concat(pathSegments).ToArray());
            var fullPath = Path.GetFullPath(combinedPath);

            if (!fullPath.StartsWith(destinationWithSeparator, DiskProviderBase.PathStringComparison))
            {
                throw new IOException($"Archive entry '{entryName}' targets a path outside the extraction folder.");
            }

            return fullPath;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static bool IsZipSymlink(ZipEntry zipEntry)
        {
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
            var typeFlag = tarEntry.TarHeader.TypeFlag;

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
