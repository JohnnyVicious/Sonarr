using System;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class HtmlMapperBaseFixture : TestBase
    {
        private Mock<IDiskProvider> _diskProvider;
        private Mock<ICacheBreakerProvider> _cacheBreakerProvider;
        private TestHtmlMapper _subject;

        public class TestHtmlMapper : HtmlMapperBase
        {
            public TestHtmlMapper(IDiskProvider diskProvider,
                                  Lazy<ICacheBreakerProvider> cacheBreakProviderFactory,
                                  Logger logger)
                : base(diskProvider, cacheBreakProviderFactory, logger)
            {
            }

            public void SetHtmlPath(string path)
            {
                HtmlPath = path;
            }

            public void SetBasePathPrefix(string basePath)
            {
                UrlBase = basePath;
            }

            public override string Map(string resourceUrl)
            {
                return HtmlPath;
            }

            public override bool CanHandle(string resourceUrl)
            {
                return true;
            }

            public string GetHtmlTextPublic(Microsoft.AspNetCore.Http.HttpContext context)
            {
                return GetHtmlText(context);
            }
        }

        [SetUp]
        public void Setup()
        {
            _diskProvider = Mocker.GetMock<IDiskProvider>();
            _cacheBreakerProvider = Mocker.GetMock<ICacheBreakerProvider>();

            _cacheBreakerProvider.Setup(c => c.AddCacheBreakerToPath(It.IsAny<string>()))
                .Returns<string>(s => s + "?h=abc123");

            _subject = new TestHtmlMapper(
                _diskProvider.Object,
                new Lazy<ICacheBreakerProvider>(() => _cacheBreakerProvider.Object),
                Mocker.Resolve<Logger>());
        }

        [Test]
        public void should_replace_url_base_placeholder()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><head><base href=\"__URL_BASE__/\" /></head></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("/sonarr");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("href=\"/sonarr/\"");
            result.Should().NotContain("__URL_BASE__");
        }

        [Test]
        public void should_replace_url_base_with_empty_string_when_no_base()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><head><base href=\"__URL_BASE__/\" /></head></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("href=\"/\"");
            result.Should().NotContain("__URL_BASE__");
        }

        [Test]
        public void should_add_cache_breaker_to_css_href()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><head><link href=\"Content/styles.css\" /></head></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("?h=abc123");
        }

        [Test]
        public void should_add_cache_breaker_to_js_src()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><body><script src=\"app.js\"></script></body></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("?h=abc123");
        }

        [Test]
        public void should_prepend_url_base_to_css_href()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><head><link href=\"Content/styles.css\" /></head></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("/sonarr");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("href=\"/sonarr/Content/styles.css");
        }

        [Test]
        public void should_prepend_url_base_to_js_src()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><body><script src=\"app.js\"></script></body></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("/sonarr");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("src=\"/sonarr/app.js");
        }

        [Test]
        public void should_not_add_cache_breaker_when_data_no_hash_present()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><head><link href=\"Content/styles.css\" data-no-hash /></head></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().NotContain("?h=abc123");
            _cacheBreakerProvider.Verify(c => c.AddCacheBreakerToPath("Content/styles.css"), Times.Never());
        }

        [Test]
        public void should_handle_png_references()
        {
            _diskProvider.Setup(d => d.ReadAllText(It.IsAny<string>()))
                .Returns("<html><body><img src=\"Content/images/logo.png\" /></body></html>");

            _subject.SetHtmlPath("/app/index.html");
            _subject.SetBasePathPrefix("");

            var result = _subject.GetHtmlTextPublic(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            result.Should().Contain("src=\"Content/images/logo.png?h=abc123\"");
        }
    }
}
