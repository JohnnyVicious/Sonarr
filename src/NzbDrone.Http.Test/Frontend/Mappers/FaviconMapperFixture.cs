using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class FaviconMapperFixture : TestBase<FaviconMapper>
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
        public void should_handle_favicon_ico()
        {
            Subject.CanHandle("/favicon.ico").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_other_ico_files()
        {
            Subject.CanHandle("/content/icon.ico").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_favicon_with_path()
        {
            Subject.CanHandle("/images/favicon.ico").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_api_url()
        {
            Subject.CanHandle("/api/v3/system").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_robots_txt()
        {
            Subject.CanHandle("/robots.txt").Should().BeFalse();
        }

        [Test]
        public void should_map_to_content_images_icons_folder()
        {
            var result = Subject.Map("/favicon.ico");

            // In debug mode the filename is favicon-debug.ico, in release it is favicon.ico.
            // BuildInfo.IsDebug determines which is used. The test just verifies the path
            // structure goes through Content/Images/Icons/.
            result.Should().Contain(Path.Combine("Content", "Images", "Icons"));
        }

        [Test]
        public void should_map_to_startup_and_ui_folder()
        {
            var result = Subject.Map("/favicon.ico");

            result.Should().StartWith(Path.Combine($"{S}opt{S}sonarr", "UI"));
        }

        [Test]
        public void should_map_to_ico_file()
        {
            var result = Subject.Map("/favicon.ico");

            result.Should().EndWith(".ico");
        }
    }
}
