using System.IO;
using System.Text;
using FluentAssertions;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ProviderTests.DiskProviderTests
{
    [TestFixture]
    public class ArchiveProviderFixture : TestBase<ArchiveService>
    {
        [Test]
        public void Should_extract_to_correct_folder()
        {
            var destinationFolder = new DirectoryInfo(GetTempFilePath());
            var testArchive = OsInfo.IsWindows ? "TestArchive.zip" : "TestArchive.tar.gz";

            Subject.Extract(GetTestPath("Files/" + testArchive), destinationFolder.FullName);

            destinationFolder.Exists.Should().BeTrue();
            destinationFolder.GetDirectories().Should().HaveCount(1);
            destinationFolder.GetDirectories("*", SearchOption.AllDirectories).Should().HaveCount(3);
            destinationFolder.GetFiles("*.*", SearchOption.AllDirectories).Should().HaveCount(6);
        }

        [Test]
        public void should_extract_zip_entries_inside_destination()
        {
            var destination = Path.Combine(TempFolder, "restore");
            var archive = CreateZipArchive("folder/file.txt", "valid");

            Subject.Extract(archive, destination);

            File.ReadAllText(Path.Combine(destination, "folder", "file.txt")).Should().Be("valid");
        }

        [TestCase("../escape.txt")]
        [TestCase("folder/../../escape.txt")]
        [TestCase("/escape.txt")]
        [TestCase(@"C:\escape.txt")]
        [TestCase(@"folder\..\escape.txt")]
        public void should_reject_zip_entries_outside_destination(string entryName)
        {
            var destination = Path.Combine(TempFolder, "restore");
            var archive = CreateZipArchive(entryName, "blocked");

            Subject.Invoking(s => s.Extract(archive, destination))
                   .Should().Throw<IOException>();

            File.Exists(Path.Combine(TempFolder, "escape.txt")).Should().BeFalse();
        }

        [Test]
        public void should_extract_tgz_entries_inside_destination()
        {
            var destination = Path.Combine(TempFolder, "restore");
            var archive = CreateTgzArchive("folder/file.txt", "valid");

            Subject.Extract(archive, destination);

            File.ReadAllText(Path.Combine(destination, "folder", "file.txt")).Should().Be("valid");
        }

        [TestCase("../escape.txt")]
        [TestCase("folder/../../escape.txt")]
        [TestCase("/escape.txt")]
        [TestCase(@"C:\escape.txt")]
        [TestCase(@"folder\..\escape.txt")]
        public void should_reject_tgz_entries_outside_destination(string entryName)
        {
            var destination = Path.Combine(TempFolder, "restore");
            var archive = CreateTgzArchive(entryName, "blocked");

            Subject.Invoking(s => s.Extract(archive, destination))
                   .Should().Throw<IOException>();

            File.Exists(Path.Combine(TempFolder, "escape.txt")).Should().BeFalse();
        }

        private string CreateZipArchive(string entryName, string contents)
        {
            var path = GetTempFilePath() + ".zip";
            var bytes = Encoding.UTF8.GetBytes(contents);

            using (var fileStream = File.Create(path))
            using (var zipStream = new ZipOutputStream(fileStream))
            {
                var zipEntry = new ZipEntry(entryName)
                {
                    Size = bytes.Length
                };

                zipStream.PutNextEntry(zipEntry);
                zipStream.Write(bytes, 0, bytes.Length);
                zipStream.CloseEntry();
            }

            return path;
        }

        private string CreateTgzArchive(string entryName, string contents)
        {
            var path = GetTempFilePath() + ".tar.gz";
            var bytes = Encoding.UTF8.GetBytes(contents);

            using (var fileStream = File.Create(path))
            using (var gzipStream = new GZipOutputStream(fileStream))
            using (var tarStream = new TarOutputStream(gzipStream, Encoding.UTF8))
            {
                var tarEntry = TarEntry.CreateTarEntry(entryName);
                tarEntry.Size = bytes.Length;

                tarStream.PutNextEntry(tarEntry);
                tarStream.Write(bytes, 0, bytes.Length);
                tarStream.CloseEntry();
            }

            return path;
        }
    }
}
