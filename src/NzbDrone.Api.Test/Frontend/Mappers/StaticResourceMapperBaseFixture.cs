using System.IO; // NOSONAR S3990: This legacy assembly needs an API-wide CLS migration.
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Backup;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Api.Test.Frontend.Mappers
{
    [TestFixture]
    public class StaticResourceMapperBaseFixture : TestBase
    {
        [Test]
        public void should_map_files_inside_root()
        {
            var root = Path.Combine(TempFolder, "Content");
            var mappedPath = Path.Combine(root, "Images", "poster.jpg");
            var mapper = new TestResourceMapper(root, mappedPath, Mocker.GetMock<IDiskProvider>().Object, TestLogger);

            mapper.Map("/Content/Images/poster.jpg").Should().Be(mappedPath);
        }

        [Test]
        public void should_reject_paths_outside_root()
        {
            var root = Path.Combine(TempFolder, "Content");
            var mappedPath = Path.Combine(root, "..", "config.xml");
            var mapper = new TestResourceMapper(root, mappedPath, Mocker.GetMock<IDiskProvider>().Object, TestLogger);

            mapper.Map("/Content/../config.xml").Should().BeNull();
        }

        [Test]
        public void should_reject_paths_next_to_root_with_same_prefix()
        {
            var root = Path.Combine(TempFolder, "Content");
            var mappedPath = Path.Combine(TempFolder, "ContentOther", "app.js");
            var mapper = new TestResourceMapper(root, mappedPath, Mocker.GetMock<IDiskProvider>().Object, TestLogger);

            mapper.Map("/ContentOther/app.js").Should().BeNull();
        }

        [Test]
        public void backup_mapper_should_handle_trailing_separator_folder()
        {
            var backupFolder = Path.Combine(TempFolder, "Backups") + Path.DirectorySeparatorChar;
            var backupFile = "sonarr_backup_2026.05.13_12.00.00.zip";

            Mocker.GetMock<IBackupService>()
                .Setup(s => s.GetBackupFolder())
                .Returns(backupFolder);

            var mapper = Mocker.Resolve<BackupFileMapper>();

            mapper.Map("/backup/" + backupFile)
                  .Should().Be(Path.Combine(backupFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), backupFile));
        }

        [Test]
        public void media_cover_mapper_should_not_probe_disk_for_paths_outside_media_cover_root()
        {
            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(s => s.AppDataFolder)
                .Returns(TempFolder);

            var mapper = Mocker.Resolve<MediaCoverMapper>();

            mapper.Map("/MediaCover/1/../../../secret.jpg").Should().BeNull();

            Mocker.GetMock<IDiskProvider>()
                .Verify(v => v.FileExists(It.IsAny<string>()), Times.Never());
        }

        private sealed class TestResourceMapper : StaticResourceMapperBase
        {
            private readonly string _folderPath;
            private readonly string _mappedPath;

            public TestResourceMapper(string folderPath, string mappedPath, IDiskProvider diskProvider, Logger logger)
                : base(diskProvider, logger)
            {
                _folderPath = folderPath;
                _mappedPath = mappedPath;
            }

            protected override string FolderPath => _folderPath;
            protected override string MapPath(string resourcePath)
            {
                return resourcePath == null ? null : _mappedPath;
            }

            public override bool CanHandle(string resourcePath)
            {
                return resourcePath != null;
            }
        }
    }
}
