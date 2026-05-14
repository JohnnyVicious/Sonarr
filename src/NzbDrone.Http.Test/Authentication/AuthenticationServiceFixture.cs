using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Sonarr.Http.Authentication;

namespace NzbDrone.Http.Test.Authentication
{
    [TestFixture]
    public class AuthenticationServiceFixture : TestBase
    {
        private Mock<IConfigFileProvider> _configFileProvider;
        private Mock<IUserService> _userService;

        private AuthenticationService CreateSubject(AuthenticationType authMethod)
        {
            _configFileProvider = new Mock<IConfigFileProvider>();
            _configFileProvider.SetupGet(c => c.AuthenticationMethod).Returns(authMethod);

            _userService = new Mock<IUserService>();

            return new AuthenticationService(_configFileProvider.Object, _userService.Object);
        }

        private HttpRequest CreateRequest()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            return httpContext.Request;
        }

        [Test]
        public void login_should_return_null_when_authentication_is_none()
        {
            var subject = CreateSubject(AuthenticationType.None);
            var request = CreateRequest();

            var result = subject.Login(request, "admin", "password");

            result.Should().BeNull();
        }

        [Test]
        public void login_should_return_user_when_credentials_are_valid()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var request = CreateRequest();
            var expectedUser = new User { Username = "admin" };

            _userService.Setup(s => s.FindUser("admin", "password"))
                .Returns(expectedUser);

            var result = subject.Login(request, "admin", "password");

            result.Should().Be(expectedUser);
        }

        [Test]
        public void login_should_return_null_when_credentials_are_invalid()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var request = CreateRequest();

            _userService.Setup(s => s.FindUser("admin", "wrongpassword"))
                .Returns((User)null);

            var result = subject.Login(request, "admin", "wrongpassword");

            result.Should().BeNull();
        }

        [Test]
        public void login_should_call_user_service_find_user()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var request = CreateRequest();

            _userService.Setup(s => s.FindUser("admin", "password"))
                .Returns(new User { Username = "admin" });

            subject.Login(request, "admin", "password");

            _userService.Verify(s => s.FindUser("admin", "password"), Times.Once());
        }

        [Test]
        public void login_should_not_call_user_service_when_auth_is_none()
        {
            var subject = CreateSubject(AuthenticationType.None);
            var request = CreateRequest();

            subject.Login(request, "admin", "password");

            _userService.Verify(s => s.FindUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void logout_should_not_throw_when_authentication_is_none()
        {
            var subject = CreateSubject(AuthenticationType.None);
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            subject.Invoking(s => s.Logout(httpContext)).Should().NotThrow();
        }

        [Test]
        public void logout_should_not_throw_when_user_is_authenticated()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }, "Forms");
            httpContext.User = new ClaimsPrincipal(identity);

            subject.Invoking(s => s.Logout(httpContext)).Should().NotThrow();
        }

        [Test]
        public void login_should_return_null_when_user_not_found()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var request = CreateRequest();

            _userService.Setup(s => s.FindUser("nonexistent", "password"))
                .Returns((User)null);

            var result = subject.Login(request, "nonexistent", "password");

            result.Should().BeNull();
        }

        [Test]
        public void log_unauthorized_should_not_throw()
        {
            var subject = CreateSubject(AuthenticationType.Forms);
            var request = CreateRequest();

            subject.Invoking(s => s.LogUnauthorized(request)).Should().NotThrow();
        }
    }
}
