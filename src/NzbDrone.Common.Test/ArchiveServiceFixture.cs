using System.IO;
using FluentAssertions;
using ICSharpCode.SharpZipLib.Zip;
using NUnit.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Common.Test
{
    [TestFixture]
    public class ArchiveServiceFixture : TestBase<ArchiveService>
    {
        private string _destinationFolder;

        [SetUp]
        public void Setup()
        {
            _destinationFolder = GetTempFilePath();
        }

        private string CreateZipWithEntry(string entryName, string content = "test content")
        {
            var zipPath = GetTempFilePath() + ".zip";
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

            using (var fs = File.Create(zipPath))
            using (var zipOutput = new ZipOutputStream(fs))
            {
                var entry = new ZipEntry(entryName);
                zipOutput.PutNextEntry(entry);

                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                zipOutput.Write(bytes, 0, bytes.Length);
                zipOutput.CloseEntry();
            }

            return zipPath;
        }

        private string CreateValidBackupZip()
        {
            var zipPath = GetTempFilePath() + ".zip";
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

            using (var fs = File.Create(zipPath))
            using (var zipOutput = new ZipOutputStream(fs))
            {
                foreach (var name in new[] { "Config.xml", "sonarr.db" })
                {
                    var entry = new ZipEntry(name);
                    zipOutput.PutNextEntry(entry);
                    var bytes = System.Text.Encoding.UTF8.GetBytes($"content of {name}");
                    zipOutput.Write(bytes, 0, bytes.Length);
                    zipOutput.CloseEntry();
                }
            }

            return zipPath;
        }

        private string GetRelativeEntryNameForOutsidePath(string outsidePath)
        {
            return NormalizeEntryName(Path.GetRelativePath(_destinationFolder, outsidePath));
        }

        private static string NormalizeEntryName(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/')
                       .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private void ExtractUnsafeZipAndAssertOutsideTargetIsUntouched(string entryName, string outsideTarget)
        {
            var zipPath = CreateZipWithEntry(entryName, "malicious");

            var exception = Assert.Throws<IOException>(() => Subject.Extract(zipPath, _destinationFolder));

            exception.Message.Should().Contain("outside");

            File.Exists(outsideTarget).Should().BeFalse(
                "unsafe ZIP entry {0} must not write outside destination directory", entryName);
        }

        [Test]
        public void should_extract_valid_zip()
        {
            var zipPath = CreateValidBackupZip();

            Subject.Extract(zipPath, _destinationFolder);

            var dir = new DirectoryInfo(_destinationFolder);
            dir.Exists.Should().BeTrue();
            dir.GetFiles().Should().HaveCount(2);
        }

        [Test]
        public void should_extract_single_file()
        {
            var zipPath = CreateZipWithEntry("test.txt", "hello");

            Subject.Extract(zipPath, _destinationFolder);

            File.Exists(Path.Combine(_destinationFolder, "test.txt")).Should().BeTrue();
            File.ReadAllText(Path.Combine(_destinationFolder, "test.txt")).Should().Be("hello");
        }

        [Test]
        public void should_extract_file_in_subdirectory()
        {
            var zipPath = CreateZipWithEntry("subdir/test.txt", "hello");

            Subject.Extract(zipPath, _destinationFolder);

            File.Exists(Path.Combine(_destinationFolder, "subdir", "test.txt")).Should().BeTrue();
        }

        [Test]
        public void should_not_write_outside_destination_with_relative_traversal()
        {
            var traversalTarget = Path.Combine(GetTempFilePath(), "evil.txt");
            var entryName = GetRelativeEntryNameForOutsidePath(traversalTarget);

            ExtractUnsafeZipAndAssertOutsideTargetIsUntouched(entryName, traversalTarget);
        }

        [Test]
        public void should_not_write_outside_destination_with_deep_traversal()
        {
            _destinationFolder = Path.Combine(GetTempFilePath(), "safe", "nested", "destination");

            var traversalTarget = Path.Combine(GetTempFilePath(), "outside", "evil.txt");
            var entryName = GetRelativeEntryNameForOutsidePath(traversalTarget);

            ExtractUnsafeZipAndAssertOutsideTargetIsUntouched(entryName, traversalTarget);
        }

        [Test]
        public void should_not_write_outside_destination_with_absolute_path()
        {
            var absolutePath = Path.Combine(GetTempFilePath(), "absolute", "evil.txt");
            var zipPath = CreateZipWithEntry(absolutePath, "malicious");
            var destinationRoot = Path.GetFullPath(_destinationFolder) + Path.DirectorySeparatorChar;

            Subject.Extract(zipPath, _destinationFolder);

            File.Exists(absolutePath).Should().BeFalse(
                "absolute ZIP entry must not write outside destination directory");

            var extractedFiles = Directory.GetFiles(_destinationFolder, "*", SearchOption.AllDirectories);
            extractedFiles.Should().NotBeEmpty();
            extractedFiles.Should().OnlyContain(file => Path.GetFullPath(file).StartsWith(destinationRoot));
        }

        [Test]
        public void should_not_write_outside_destination_with_mixed_separators()
        {
            var traversalTarget = Path.Combine(GetTempFilePath(), "mixed", "evil.txt");
            var entryName = GetRelativeEntryNameForOutsidePath(traversalTarget)
                .Replace('/', '\\');

            ExtractUnsafeZipAndAssertOutsideTargetIsUntouched(entryName, traversalTarget);
        }

        [Test]
        public void should_sanitize_zip_entry_with_leading_slash()
        {
            var zipPath = CreateZipWithEntry("/subdir/test.txt", "hello");
            var expectedExtractedFile = Path.Combine(_destinationFolder, "subdir", "test.txt");
            var rootLevelTarget = Path.Combine(Path.GetPathRoot(_destinationFolder), "subdir", "test.txt");

            Subject.Extract(zipPath, _destinationFolder);

            File.Exists(expectedExtractedFile).Should().BeTrue();
            File.ReadAllText(expectedExtractedFile).Should().Be("hello");
            File.Exists(rootLevelTarget).Should().BeFalse(
                "leading slash ZIP entry must not write to the filesystem root");
            Directory.GetFiles(_destinationFolder, "*", SearchOption.AllDirectories).Should().HaveCount(1);
        }

        [Test]
        public void should_not_extract_traversal_disguised_in_nested_path()
        {
            var escapePath = Path.GetFullPath(Path.Combine(_destinationFolder, "valid", "..", "..", "escape.txt"));

            ExtractUnsafeZipAndAssertOutsideTargetIsUntouched("valid/../../escape.txt", escapePath);
        }

        [Test]
        public void should_create_zip_with_files()
        {
            var sourceDir = GetTempFilePath();
            Directory.CreateDirectory(sourceDir);

            var file1 = Path.Combine(sourceDir, "file1.txt");
            var file2 = Path.Combine(sourceDir, "file2.txt");
            File.WriteAllText(file1, "content1");
            File.WriteAllText(file2, "content2");

            var zipPath = GetTempFilePath() + ".zip";
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

            Subject.CreateZip(zipPath, new[] { file1, file2 });

            File.Exists(zipPath).Should().BeTrue();

            // Verify the zip uses only filenames (not full paths) as entry names
            using (var fs = File.OpenRead(zipPath))
            {
                var zip = new ZipFile(fs);
                zip.Count.Should().Be(2);
                zip.FindEntry("file1.txt", true).Should().BeGreaterOrEqualTo(0);
                zip.FindEntry("file2.txt", true).Should().BeGreaterOrEqualTo(0);
            }
        }
    }
}
