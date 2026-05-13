using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Backup;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class BackupFileMapperFixture : TestBase<BackupFileMapper>
    {
        private static readonly char S = Path.DirectorySeparatorChar;
        private const string BACKUP_FOLDER = "/backups";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IBackupService>()
                  .Setup(c => c.GetBackupFolder())
                  .Returns(BACKUP_FOLDER);
        }

        [TestCase("/backup/sonarr_backup_v5.0.0_2024.01.01.zip")]
        [TestCase("/backup/sonarr_backup_2024.01.01.zip")]
        [TestCase("/backup/nzbdrone_backup_v3.0.0_2020.05.15.zip")]
        public void should_handle_valid_backup_urls(string url)
        {
            Subject.CanHandle(url).Should().BeTrue();
        }

        [Test]
        public void should_handle_backup_url_case_insensitive()
        {
            Subject.CanHandle("/backup/Sonarr_Backup_v5.0.0_2024.01.01.zip").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_non_zip_file()
        {
            Subject.CanHandle("/backup/evil.txt").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_arbitrary_zip_file()
        {
            Subject.CanHandle("/backup/random.zip").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_url_not_starting_with_backup()
        {
            Subject.CanHandle("/api/backup/sonarr_backup_v5.0.0_2024.01.01.zip").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_backup_root()
        {
            Subject.CanHandle("/backup/").Should().BeFalse();
        }

        [Test]
        public void should_map_backup_filename_to_backup_folder()
        {
            var result = Subject.Map("/backup/sonarr_backup_v5.0.0_2024.01.01.zip");

            result.Should().Be(Path.Combine(BACKUP_FOLDER, "sonarr_backup_v5.0.0_2024.01.01.zip"));
        }

        [Test]
        public void should_map_nzbdrone_backup_filename()
        {
            var result = Subject.Map("/backup/nzbdrone_backup_v3.0.0_2020.05.15.zip");

            result.Should().Be(Path.Combine(BACKUP_FOLDER, "nzbdrone_backup_v3.0.0_2020.05.15.zip"));
        }

        [Test]
        public void should_map_path_traversal_without_sanitizing()
        {
            // NOTE: BackupFileMapper does NOT use Path.GetFileName() to extract the filename.
            // It only strips the "/backup/" prefix and replaces slashes. Path traversal
            // sequences like /../../../etc/passwd pass through and resolve outside
            // the backup folder. However, CanHandle() would reject this URL because
            // it does not match BackupFileRegex, so in practice this path is not reachable
            // through normal request routing.
            var result = Subject.Map("/backup/../../../etc/passwd");

            var fullPath = Path.GetFullPath(result);
            var backupDir = Path.GetFullPath(BACKUP_FOLDER);

            fullPath.Should().NotStartWith(backupDir);
        }
    }
}
