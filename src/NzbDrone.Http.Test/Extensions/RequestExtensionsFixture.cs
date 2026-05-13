using System.Net; // NOSONAR
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Sonarr.Http.Extensions;

namespace NzbDrone.Http.Test.Extensions
{
    [TestFixture]
    public class RequestExtensionsFixture : TestBase
    {
        [Test]
        public void is_api_request_should_return_true_for_api_path()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/series";

            context.Request.IsApiRequest().Should().BeTrue();
        }

        [Test]
        public void is_api_request_should_return_false_for_non_api_path()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/content/styles.css";

            context.Request.IsApiRequest().Should().BeFalse();
        }

        [Test]
        public void is_api_request_should_be_case_insensitive()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/API/v3/series";

            context.Request.IsApiRequest().Should().BeTrue();
        }

        [Test]
        public void is_favicon_request_should_return_true_for_favicon()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/favicon.ico";

            context.Request.IsFavIconRequest().Should().BeTrue();
        }

        [Test]
        public void is_favicon_request_should_return_false_for_other_paths()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/content/icon.ico";

            context.Request.IsFavIconRequest().Should().BeFalse();
        }

        [Test]
        public void get_boolean_query_parameter_should_return_true_when_set()
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?includeImages=true");

            context.Request.GetBooleanQueryParameter("includeImages").Should().BeTrue();
        }

        [Test]
        public void get_boolean_query_parameter_should_return_false_when_set_to_false()
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?includeImages=false");

            context.Request.GetBooleanQueryParameter("includeImages").Should().BeFalse();
        }

        [Test]
        public void get_boolean_query_parameter_should_return_default_when_missing()
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?other=value");

            context.Request.GetBooleanQueryParameter("includeImages", true).Should().BeTrue();
        }

        [Test]
        public void get_boolean_query_parameter_should_return_false_as_default_when_missing()
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("");

            context.Request.GetBooleanQueryParameter("includeImages").Should().BeFalse();
        }

        [Test]
        public void get_remote_ip_should_return_ip_from_context()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100"); // NOSONAR

            context.GetRemoteIP().Should().Be("192.168.1.100"); // NOSONAR
        }

        [Test]
        public void get_remote_ip_should_return_unknown_for_null_context()
        {
            ((HttpContext)null).GetRemoteIP().Should().Be("Unknown");
        }

        [Test]
        public void get_remote_ip_should_map_ipv6_to_ipv4()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1").MapToIPv6(); // NOSONAR

            context.GetRemoteIP().Should().Be("192.168.1.1"); // NOSONAR
        }

        [Test]
        public void get_source_should_return_sonarr_client_header_when_present()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Sonarr-Client"] = "SonarrUI";

            context.Request.GetSource().Should().Be("SonarrUI");
        }

        [Test]
        public void disable_cache_should_set_no_cache_headers()
        {
            var context = new DefaultHttpContext();
            context.Response.Headers["Last-Modified"] = "some-value";

            context.Response.Headers.DisableCache();

            context.Response.Headers.ContainsKey("Last-Modified").Should().BeFalse();
            context.Response.Headers["Cache-Control"].ToString().Should().Be("no-cache, no-store");
            context.Response.Headers["Expires"].ToString().Should().Be("-1");
            context.Response.Headers["Pragma"].ToString().Should().Be("no-cache");
        }

        [Test]
        public void enable_cache_should_set_cache_headers()
        {
            var context = new DefaultHttpContext();

            context.Response.Headers.EnableCache();

            context.Response.Headers["Cache-Control"].ToString().Should().Be("max-age=31536000, public");
            context.Response.Headers.ContainsKey("Last-Modified").Should().BeTrue();
        }
    }
}
