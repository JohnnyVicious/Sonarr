using FluentAssertions; //NOSONAR S3990: This legacy assembly needs an API-wide CLS migration.
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.MediaCovers;

namespace NzbDrone.Api.Test.v3.MediaCovers
{
    [TestFixture]
    public class MediaCoverControllerFixture : TestBase<MediaCoverController>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(s => s.AppDataFolder)
                .Returns(TempFolder);
        }

        [Test]
        public void should_reject_media_cover_paths_outside_series_cover_folder()
        {
            var result = Subject.GetMediaCover(1, "../../../secret.jpg");

            result.Should().BeOfType<NotFoundResult>();

            Mocker.GetMock<IDiskProvider>()
                .Verify(v => v.FileExists(It.IsAny<string>()), Times.Never());
        }
    }
}
