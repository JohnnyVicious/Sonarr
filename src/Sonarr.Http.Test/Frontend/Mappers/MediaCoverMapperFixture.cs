using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace Sonarr.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class MediaCoverMapperFixture : TestBase<MediaCoverMapper>
    {
        private string _appDataFolder;

        [SetUp]
        public void Setup()
        {
            _appDataFolder = Path.Combine(TempFolder, "appdata");

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(s => s.AppDataFolder)
                .Returns(_appDataFolder);
        }

        [Test]
        public void should_not_probe_disk_when_resolved_path_leaves_media_cover_root()
        {
            Subject.Map("/MediaCover/1/../../../secret.jpg").Should().BeNull();

            Mocker.GetMock<IDiskProvider>()
                .Verify(v => v.FileExists(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_fall_back_to_original_cover_when_resized_cover_is_missing()
        {
            var resizedCover = Path.Combine(_appDataFolder, "MediaCover", "1", "poster-250.jpg");
            var originalCover = Path.Combine(_appDataFolder, "MediaCover", "1", "poster.jpg");

            Mocker.GetMock<IDiskProvider>()
                .Setup(v => v.FileExists(resizedCover))
                .Returns(false);

            Subject.Map("/MediaCover/1/poster-250.jpg").Should().Be(originalCover);
        }
    }
}
