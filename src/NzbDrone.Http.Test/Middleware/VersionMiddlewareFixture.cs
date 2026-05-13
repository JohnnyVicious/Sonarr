using System.Threading.Tasks; // NOSONAR
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Http.Test.Middleware
{
    [TestFixture]
    public class VersionMiddlewareFixture : TestBase
    {
        private Sonarr.Http.Middleware.VersionMiddleware _subject;
        private bool _nextCalled;

        private RequestDelegate CreateNext()
        {
            _nextCalled = false;
            return (ctx) =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            };
        }

        [SetUp]
        public void Setup()
        {
            var next = CreateNext();
            _subject = new Sonarr.Http.Middleware.VersionMiddleware(next);
        }

        [Test]
        public async Task should_add_version_header_to_api_request()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await _subject.InvokeAsync(context);

            context.Response.Headers.ContainsKey("X-Application-Version").Should().BeTrue();
            context.Response.Headers["X-Application-Version"].ToString().Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task should_not_add_version_header_to_non_api_request()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/content/styles.css";

            await _subject.InvokeAsync(context);

            context.Response.Headers.ContainsKey("X-Application-Version").Should().BeFalse();
        }

        [Test]
        public async Task should_call_next_middleware_for_api_request()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await _subject.InvokeAsync(context);

            _nextCalled.Should().BeTrue();
        }

        [Test]
        public async Task should_call_next_middleware_for_non_api_request()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/content/styles.css";

            await _subject.InvokeAsync(context);

            _nextCalled.Should().BeTrue();
        }

        [Test]
        public async Task should_not_overwrite_existing_version_header()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";
            context.Response.Headers["X-Application-Version"] = "existing-version";

            await _subject.InvokeAsync(context);

            context.Response.Headers["X-Application-Version"].ToString().Should().Be("existing-version");
        }

        [Test]
        public async Task should_not_add_header_to_favicon_request()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/favicon.ico";

            await _subject.InvokeAsync(context);

            context.Response.Headers.ContainsKey("X-Application-Version").Should().BeFalse();
        }

        [Test]
        public async Task should_add_header_to_api_subroutes()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/episode/1";

            await _subject.InvokeAsync(context);

            context.Response.Headers.ContainsKey("X-Application-Version").Should().BeTrue();
        }
    }
}
