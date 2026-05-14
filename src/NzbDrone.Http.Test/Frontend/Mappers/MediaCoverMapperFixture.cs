using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class MediaCoverMapperFixture : TestBase<MediaCoverMapper>
    {
        private static readonly char S = Path.DirectorySeparatorChar;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(c => c.AppDataFolder)
                  .Returns($"{S}app{S}data");
        }

        [Test]
        public void should_handle_media_cover_url()
        {
            Subject.CanHandle("/MediaCover/1/poster.jpg").Should().BeTrue();
        }

        [Test]
        public void should_handle_media_cover_url_case_insensitive()
        {
            Subject.CanHandle("/mediacover/1/poster.jpg").Should().BeTrue();
        }

        [Test]
        public void should_handle_media_cover_url_upper_case()
        {
            Subject.CanHandle("/MEDIACOVER/1/poster.jpg").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_api_url()
        {
            Subject.CanHandle("/api/v3/series").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_content_url()
        {
            Subject.CanHandle("/content/styles.css").Should().BeFalse();
        }

        [Test]
        public void should_map_normal_path()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetFileSize(It.IsAny<string>()))
                  .Returns(1000);

            var result = Subject.Map("/MediaCover/1/poster.jpg");

            result.Should().Be($"{S}app{S}data{S}MediaCover{S}1{S}poster.jpg");
        }

        [Test]
        public void should_return_direct_path_when_file_exists_and_size_greater_than_zero()
        {
            var expectedPath = $"{S}app{S}data{S}MediaCover{S}1{S}poster.jpg";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(expectedPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetFileSize(expectedPath))
                  .Returns(5000);

            var result = Subject.Map("/MediaCover/1/poster.jpg");

            result.Should().Be(expectedPath);
        }

        [Test]
        public void should_fallback_to_original_when_resized_image_does_not_exist()
        {
            var resizedPath = $"{S}app{S}data{S}MediaCover{S}1{S}poster-300.jpg";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(resizedPath))
                  .Returns(false);

            var result = Subject.Map("/MediaCover/1/poster-300.jpg");

            // When resized file doesn't exist, it should fall back to the non-resized version
            result.Should().Be($"{S}app{S}data{S}MediaCover{S}1{S}poster.jpg");
        }

        [Test]
        public void should_fallback_to_original_when_resized_image_has_zero_size()
        {
            var resizedPath = $"{S}app{S}data{S}MediaCover{S}1{S}poster-300.jpg";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(resizedPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetFileSize(resizedPath))
                  .Returns(0);

            var result = Subject.Map("/MediaCover/1/poster-300.jpg");

            result.Should().Be($"{S}app{S}data{S}MediaCover{S}1{S}poster.jpg");
        }

        [Test]
        public void should_not_fallback_for_non_resized_image_that_does_not_exist()
        {
            var path = $"{S}app{S}data{S}MediaCover{S}1{S}poster.jpg";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(path))
                  .Returns(false);

            var result = Subject.Map("/MediaCover/1/poster.jpg");

            // No fallback for non-resized images, returns the original path
            result.Should().Be(path);
        }
    }
}
