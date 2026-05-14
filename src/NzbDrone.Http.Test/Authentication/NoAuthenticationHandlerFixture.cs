using System.Text.Encodings.Web;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using Sonarr.Http.Authentication;

namespace NzbDrone.Http.Test.Authentication
{
    [TestFixture]
    public class NoAuthenticationHandlerFixture
    {
        private NoAuthenticationHandler _handler;
        private DefaultHttpContext _httpContext;

        [SetUp]
        public async Task Setup()
        {
            _httpContext = new DefaultHttpContext();

            var options = new AuthenticationSchemeOptions();

            var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(options);
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);

            var loggerFactory = new NullLoggerFactory();

            _handler = new NoAuthenticationHandler(
                optionsMonitor.Object,
                loggerFactory,
                UrlEncoder.Default);

            var scheme = new AuthenticationScheme(
                "NoAuth",
                "NoAuth",
                typeof(NoAuthenticationHandler));

            await _handler.InitializeAsync(scheme, _httpContext);
        }

        [Test]
        public async Task should_always_return_success()
        {
            var result = await _handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
        }

        [Test]
        public async Task should_have_anonymous_user_claim()
        {
            var result = await _handler.AuthenticateAsync();

            result.Principal.Claims
                .Should().Contain(c => c.Type == "user" && c.Value == "Anonymous");
        }

        [Test]
        public async Task should_have_authentication_type_none_claim()
        {
            var result = await _handler.AuthenticateAsync();

            result.Principal.Claims
                .Should().Contain(c => c.Type == "AuthType" && c.Value == AuthenticationType.None.ToString());
        }

        [Test]
        public async Task should_have_noauth_identity_type()
        {
            var result = await _handler.AuthenticateAsync();

            result.Principal.Identity.AuthenticationType.Should().Be("NoAuth");
        }

        [Test]
        public async Task should_have_noauth_ticket_scheme()
        {
            var result = await _handler.AuthenticateAsync();

            result.Ticket.AuthenticationScheme.Should().Be("NoAuth");
        }

        [Test]
        public async Task should_have_authenticated_identity()
        {
            var result = await _handler.AuthenticateAsync();

            result.Principal.Identity.IsAuthenticated.Should().BeTrue();
        }

        [Test]
        public async Task should_have_user_as_name_claim_type()
        {
            var result = await _handler.AuthenticateAsync();

            result.Principal.Identity.Name.Should().Be("Anonymous");
        }

        [Test]
        public async Task should_have_non_null_ticket()
        {
            var result = await _handler.AuthenticateAsync();

            result.Ticket.Should().NotBeNull();
        }
    }
}
