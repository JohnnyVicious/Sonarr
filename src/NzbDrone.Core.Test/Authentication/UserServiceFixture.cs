using System; // NOSONAR
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Authentication
{
    [TestFixture]
    public class UserServiceFixture : CoreTest<UserService>
    {
        [Test]
        public void should_add_user_with_hashed_password()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.Insert(It.IsAny<User>()))
                  .Returns<User>(u => u);

            var result = Subject.Add("admin", "password123");

            result.Username.Should().Be("admin");
            result.Password.Should().NotBe("password123");
            result.Salt.Should().NotBeNullOrWhiteSpace();
            result.Iterations.Should().BeGreaterThan(0);
            result.Identifier.Should().NotBeEmpty();

            Mocker.GetMock<IUserRepository>()
                  .Verify(v => v.Insert(It.Is<User>(u => u.Username == "admin")), Times.Once());
        }

        [Test]
        public void should_add_user_with_lowercase_username()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.Insert(It.IsAny<User>()))
                  .Returns<User>(u => u);

            var result = Subject.Add("Admin", "password");

            result.Username.Should().Be("admin");
        }

        [Test]
        public void should_update_user()
        {
            var user = new User { Id = 1, Username = "admin", Password = "hashed" };

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.Update(user))
                  .Returns(user);

            var result = Subject.Update(user);

            result.Should().Be(user);

            Mocker.GetMock<IUserRepository>()
                  .Verify(v => v.Update(user), Times.Once());
        }

        [Test]
        public void should_find_user_returns_single_user()
        {
            var user = new User { Id = 1, Username = "admin" };

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.SingleOrDefault())
                  .Returns(user);

            var result = Subject.FindUser();

            result.Should().Be(user);
        }

        [Test]
        public void should_return_null_when_no_user_exists()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.SingleOrDefault())
                  .Returns((User)null);

            var result = Subject.FindUser();

            result.Should().BeNull();
        }

        [Test]
        public void should_return_null_when_username_is_empty()
        {
            var result = Subject.FindUser("", "password");

            result.Should().BeNull();
        }

        [Test]
        public void should_return_null_when_password_is_empty()
        {
            var result = Subject.FindUser("admin", "");

            result.Should().BeNull();
        }

        [Test]
        public void should_return_null_when_user_not_found_by_username()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.FindUser("unknown"))
                  .Returns((User)null);

            var result = Subject.FindUser("unknown", "password");

            result.Should().BeNull();
        }

        [Test]
        public void should_find_user_by_guid_identifier()
        {
            var identifier = Guid.NewGuid();
            var user = new User { Id = 1, Username = "admin", Identifier = identifier };

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.FindUser(identifier))
                  .Returns(user);

            var result = Subject.FindUser(identifier);

            result.Should().Be(user);
            result.Identifier.Should().Be(identifier);
        }

        [Test]
        public void should_upsert_create_new_user_when_none_exists()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.SingleOrDefault())
                  .Returns((User)null);

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.Insert(It.IsAny<User>()))
                  .Returns<User>(u => u);

            var result = Subject.Upsert("newuser", "newpass");

            result.Username.Should().Be("newuser");

            Mocker.GetMock<IUserRepository>()
                  .Verify(v => v.Insert(It.IsAny<User>()), Times.Once());
        }

        [Test]
        public void should_upsert_update_existing_user()
        {
            var existingUser = new User
            {
                Id = 1,
                Username = "olduser",
                Password = "oldpasshash",
                Salt = Convert.ToBase64String(new byte[16]),
                Iterations = 10000,
                Identifier = Guid.NewGuid()
            };

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.SingleOrDefault())
                  .Returns(existingUser);

            Mocker.GetMock<IUserRepository>()
                  .Setup(s => s.Update(It.IsAny<User>()))
                  .Returns<User>(u => u);

            var result = Subject.Upsert("updateduser", "newpassword");

            result.Username.Should().Be("updateduser");

            Mocker.GetMock<IUserRepository>()
                  .Verify(v => v.Update(It.IsAny<User>()), Times.Once());
        }
    }
}
