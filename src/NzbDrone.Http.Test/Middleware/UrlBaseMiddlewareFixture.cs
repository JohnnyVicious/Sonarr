using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Http.Test.Middleware
{
    [TestFixture]
    public class UrlBaseMiddlewareFixture : TestBase
    {
        private bool _nextCalled;

        private RequestDelegate CreateNext()
        {
            _nextCalled = false;
            return ctx =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            };
        }

        [Test]
        public async Task should_redirect_when_url_base_set_and_path_base_empty()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "/sonarr");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(307);
            _nextCalled.Should().BeFalse();
        }

        [Test]
        public async Task should_redirect_to_correct_url_with_url_base()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "/sonarr");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await middleware.InvokeAsync(context);

            context.Response.Headers["Location"].ToString().Should().Be("/sonarr/api/v3/series");
        }

        [Test]
        public async Task should_include_query_string_in_redirect()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "/sonarr");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";
            context.Request.QueryString = new QueryString("?page=1&pageSize=10");

            await middleware.InvokeAsync(context);

            context.Response.Headers["Location"].ToString().Should().Be("/sonarr/api/v3/series?page=1&pageSize=10");
        }

        [Test]
        public async Task should_call_next_when_path_base_is_set()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "/sonarr");

            var context = new DefaultHttpContext();
            context.Request.PathBase = "/sonarr";
            context.Request.Path = "/api/v3/series";

            await middleware.InvokeAsync(context);

            _nextCalled.Should().BeTrue();
        }

        [Test]
        public async Task should_call_next_when_url_base_is_empty()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await middleware.InvokeAsync(context);

            _nextCalled.Should().BeTrue();
        }

        [Test]
        public async Task should_call_next_when_url_base_is_null()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, null);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            await middleware.InvokeAsync(context);

            _nextCalled.Should().BeTrue();
        }

        [Test]
        public async Task should_not_call_next_when_redirecting()
        {
            var next = CreateNext();
            var middleware = new Sonarr.Http.Middleware.UrlBaseMiddleware(next, "/sonarr");

            var context = new DefaultHttpContext();
            context.Request.Path = "/";

            await middleware.InvokeAsync(context);

            _nextCalled.Should().BeFalse();
            context.Response.StatusCode.Should().Be(307);
            context.Response.Headers["Location"].ToString().Should().StartWith("/sonarr/");
        }
    }
}
