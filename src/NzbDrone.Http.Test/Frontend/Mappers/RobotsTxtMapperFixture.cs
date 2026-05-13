using System.IO; // NOSONAR
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class RobotsTxtMapperFixture : TestBase<RobotsTxtMapper>
    {
        private static readonly char S = Path.DirectorySeparatorChar;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(c => c.StartUpFolder)
                  .Returns($"{S}opt{S}sonarr");

            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(c => c.UiFolder)
                  .Returns("UI");
        }

        [Test]
        public void should_handle_robots_txt()
        {
            Subject.CanHandle("/robots.txt").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_robots_txt_in_subdirectory()
        {
            Subject.CanHandle("/content/robots.txt").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_favicon_ico()
        {
            Subject.CanHandle("/favicon.ico").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_api_url()
        {
            Subject.CanHandle("/api/v3/system").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_other_txt_files()
        {
            Subject.CanHandle("/readme.txt").Should().BeFalse();
        }

        [Test]
        public void should_map_to_content_robots_txt()
        {
            var result = Subject.Map("/robots.txt");

            result.Should().Be(Path.Combine($"{S}opt{S}sonarr", "UI", "Content", "robots.txt"));
        }

        [Test]
        public void should_map_to_startup_and_ui_folder()
        {
            var result = Subject.Map("/robots.txt");

            result.Should().StartWith(Path.Combine($"{S}opt{S}sonarr", "UI"));
        }

        [Test]
        public void should_map_to_txt_file()
        {
            var result = Subject.Map("/robots.txt");

            result.Should().EndWith("robots.txt");
        }
    }
}
