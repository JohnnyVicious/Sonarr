using System;
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
            var zipPath = Path.Combine(GetTempFilePath() + ".zip");
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
            var zipPath = Path.Combine(GetTempFilePath() + ".zip");
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
            var zipPath = CreateZipWithEntry("../../evil.txt", "malicious");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            // The extraction should either throw or the file should not exist outside destination
            // Currently this is a known vulnerability - the test documents the expected secure behavior
            try
            {
                act();
            }
            catch (IOException)
            {
                // Throwing is acceptable secure behavior
                return;
            }

            // If it didn't throw, verify the file was NOT written outside the destination
            var traversalTarget = Path.GetFullPath(Path.Combine(_destinationFolder, "../../evil.txt"));
            File.Exists(traversalTarget).Should().BeFalse(
                "ZIP entry with relative traversal should not write outside destination directory");
        }

        [Test]
        public void should_not_write_outside_destination_with_deep_traversal()
        {
            var zipPath = CreateZipWithEntry("../../../../../../../tmp/evil.txt", "malicious");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            try
            {
                act();
            }
            catch (IOException)
            {
                return;
            }

            var evilPath = Path.Combine(Path.GetTempPath(), "evil.txt");
            File.Exists(evilPath).Should().BeFalse(
                "ZIP entry with deep traversal should not write outside destination directory");
        }

        [Test]
        public void should_not_write_outside_destination_with_absolute_path()
        {
            var absolutePath = Path.Combine(Path.GetTempPath(), "sonarr_test_evil_absolute.txt");
            var zipPath = CreateZipWithEntry(absolutePath, "malicious");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            try
            {
                act();
            }
            catch (IOException)
            {
                return;
            }

            File.Exists(absolutePath).Should().BeFalse(
                "ZIP entry with absolute path should not write outside destination directory");
        }

        [Test]
        public void should_not_write_outside_destination_with_mixed_separators()
        {
            var zipPath = CreateZipWithEntry("..\\..\\evil.txt", "malicious");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            try
            {
                act();
            }
            catch (IOException)
            {
                return;
            }

            var traversalTarget = Path.GetFullPath(Path.Combine(_destinationFolder, "..\\..\\evil.txt"));
            File.Exists(traversalTarget).Should().BeFalse(
                "ZIP entry with backslash traversal should not write outside destination directory");
        }

        [Test]
        public void should_handle_zip_entry_with_leading_slash()
        {
            var zipPath = CreateZipWithEntry("/subdir/test.txt", "hello");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            try
            {
                act();
            }
            catch (IOException)
            {
                return;
            }

            var resolvedPath = Path.GetFullPath(Path.Combine(_destinationFolder, "subdir", "test.txt"));
            var destinationRoot = Path.GetFullPath(_destinationFolder) + Path.DirectorySeparatorChar;

            if (File.Exists(resolvedPath))
            {
                resolvedPath.StartsWith(destinationRoot).Should().BeTrue(
                    "extracted file must remain within the destination directory");
            }
        }

        [Test]
        public void should_not_extract_traversal_disguised_in_nested_path()
        {
            var zipPath = CreateZipWithEntry("valid/../../escape.txt", "malicious");

            var act = () => Subject.Extract(zipPath, _destinationFolder);

            try
            {
                act();
            }
            catch (IOException)
            {
                return;
            }

            var escapePath = Path.GetFullPath(Path.Combine(_destinationFolder, "valid/../../escape.txt"));
            var destinationRoot = Path.GetFullPath(_destinationFolder) + Path.DirectorySeparatorChar;

            if (File.Exists(escapePath))
            {
                escapePath.StartsWith(destinationRoot).Should().BeTrue(
                    "nested traversal entry should not escape destination directory");
            }
        }

        [Test]
        public void should_throw_on_corrupt_zip()
        {
            var corruptPath = Path.Combine(GetTempFilePath() + ".zip");
            Directory.CreateDirectory(Path.GetDirectoryName(corruptPath));
            File.WriteAllText(corruptPath, "this is not a zip file");

            Assert.Throws<IOException>(() => Subject.Extract(corruptPath, _destinationFolder));
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

            var zipPath = Path.Combine(GetTempFilePath() + ".zip");
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
