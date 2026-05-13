using System.Text.Encodings.Web; // NOSONAR
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using Sonarr.Http.Authentication;

namespace NzbDrone.Http.Test.Authentication
{
    [TestFixture]
    public class ApiKeyAuthenticationHandlerFixture
    {
        // nosemgrep: codacy.csharp.security.hard-coded-password
        private const string API_KEY = "test-api-key-1234";

        private ApiKeyAuthenticationHandler _handler;
        private DefaultHttpContext _httpContext;

        [SetUp]
        public async Task Setup()
        {
            _httpContext = new DefaultHttpContext();

            var options = new ApiKeyAuthenticationOptions
            {
                HeaderName = "X-Api-Key",
                QueryName = "apikey"
            };

            var optionsMonitor = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
            optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(options);
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);

            var loggerFactory = new NullLoggerFactory();

            var configFileProvider = new Mock<IConfigFileProvider>();
            configFileProvider.SetupGet(c => c.ApiKey).Returns(API_KEY);

            _handler = new ApiKeyAuthenticationHandler(
                optionsMonitor.Object,
                loggerFactory,
                UrlEncoder.Default,
                configFileProvider.Object);

            var scheme = new AuthenticationScheme(
                ApiKeyAuthenticationOptions.DefaultScheme,
                ApiKeyAuthenticationOptions.DefaultScheme,
                typeof(ApiKeyAuthenticationHandler));

            await _handler.InitializeAsync(scheme, _httpContext);
        }

        [Test]
        public async Task should_succeed_with_valid_api_key_in_header()
        {
            _httpContext.Request.Headers["X-Api-Key"] = API_KEY;

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
            result.Ticket.Should().NotBeNull();
            result.Principal.Claims.Should().Contain(c => c.Type == "ApiKey" && c.Value == "true");
        }

        [Test]
        public async Task should_succeed_with_valid_api_key_in_query_parameter()
        {
            _httpContext.Request.QueryString = new QueryString($"?apikey={API_KEY}");

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
            result.Ticket.Should().NotBeNull();
        }

        [Test]
        public async Task should_succeed_with_valid_api_key_in_authorization_bearer_header()
        {
            _httpContext.Request.Headers["Authorization"] = $"Bearer {API_KEY}";

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
            result.Ticket.Should().NotBeNull();
        }

        [Test]
        public async Task should_return_no_result_with_invalid_api_key_in_header()
        {
            _httpContext.Request.Headers["X-Api-Key"] = "wrong-api-key";

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
        }

        [Test]
        public async Task should_return_no_result_when_no_api_key_provided()
        {
            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
        }

        [Test]
        public async Task should_return_no_result_when_api_key_is_empty()
        {
            _httpContext.Request.Headers["X-Api-Key"] = string.Empty;

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
        }

        [Test]
        public async Task should_return_no_result_when_api_key_is_whitespace()
        {
            _httpContext.Request.Headers["X-Api-Key"] = "   ";

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
        }

        [Test]
        public async Task should_prefer_query_parameter_over_header()
        {
            _httpContext.Request.QueryString = new QueryString($"?apikey={API_KEY}");
            _httpContext.Request.Headers["X-Api-Key"] = "wrong-api-key";

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
        }

        [Test]
        public async Task should_set_authentication_ticket_scheme()
        {
            _httpContext.Request.Headers["X-Api-Key"] = API_KEY;

            var result = await _handler.AuthenticateAsync();

            result.Ticket.AuthenticationScheme.Should().Be(ApiKeyAuthenticationOptions.DefaultScheme);
        }

        [Test]
        public async Task should_set_identity_authentication_type()
        {
            _httpContext.Request.Headers["X-Api-Key"] = API_KEY;

            var result = await _handler.AuthenticateAsync();

            result.Principal.Identity.AuthenticationType.Should().Be(ApiKeyAuthenticationOptions.DefaultScheme);
        }

        [Test]
        public async Task should_return_no_result_with_invalid_bearer_token()
        {
            _httpContext.Request.Headers["Authorization"] = "Bearer invalid-key";

            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
        }

        [Test]
        public async Task should_set_challenge_response_to_401()
        {
            await _handler.ChallengeAsync(null);

            _httpContext.Response.StatusCode.Should().Be(401);
        }

        [Test]
        public async Task should_set_forbid_response_to_403()
        {
            await _handler.ForbidAsync(null);

            _httpContext.Response.StatusCode.Should().Be(403);
        }
    }
}
