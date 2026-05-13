using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class StaticResourceMapperFixture : TestBase<StaticResourceMapper>
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

        [TestCase("/content/styles.css")]
        [TestCase("/content/images/logo.png")]
        public void should_handle_content_urls(string url)
        {
            Subject.CanHandle(url).Should().BeTrue();
        }

        [Test]
        public void should_handle_js_files()
        {
            Subject.CanHandle("/app.js").Should().BeTrue();
        }

        [Test]
        public void should_handle_map_files()
        {
            Subject.CanHandle("/main.js.map").Should().BeTrue();
        }

        [Test]
        public void should_handle_css_files()
        {
            Subject.CanHandle("/app.css").Should().BeTrue();
        }

        [Test]
        public void should_handle_swf_files()
        {
            Subject.CanHandle("/player.swf").Should().BeTrue();
        }

        [Test]
        public void should_handle_oauth_html()
        {
            Subject.CanHandle("/oauth.html").Should().BeTrue();
        }

        [Test]
        public void should_handle_ico_files_that_are_not_favicon()
        {
            Subject.CanHandle("/content/icon.ico").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_favicon_ico()
        {
            Subject.CanHandle("/favicon.ico").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_content_manifest()
        {
            Subject.CanHandle("/content/manifest.json").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_content_browserconfig()
        {
            Subject.CanHandle("/content/browserconfig.xml").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_api_urls()
        {
            Subject.CanHandle("/api/v3/series").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_html_files()
        {
            Subject.CanHandle("/index.html").Should().BeFalse();
        }

        [Test]
        public void should_map_content_path_to_startup_folder()
        {
            var result = Subject.Map("/content/styles.css");

            result.Should().Be(Path.Combine($"{S}opt{S}sonarr", "UI", "content", "styles.css"));
        }

        [Test]
        public void should_map_js_path_to_startup_folder()
        {
            var result = Subject.Map("/app.js");

            result.Should().Be(Path.Combine($"{S}opt{S}sonarr", "UI", "app.js"));
        }
    }
}
