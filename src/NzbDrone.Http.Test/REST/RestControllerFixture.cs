using FluentAssertions; // NOSONAR
using NUnit.Framework;
using NzbDrone.Test.Common;
using Sonarr.Http.REST;

namespace NzbDrone.Http.Test.REST
{
    [TestFixture]
    public class RestControllerFixture : TestBase
    {
        public class TestResource : RestResource
        {
            public string Name { get; set; }
        }

        public class TestController : RestController<TestResource>
        {
            public new void ValidateId(int id)
            {
                // nosemgrep: codacy.csharp.security.null-dereference
                base.ValidateId(id);
            }

            public new void ValidateResource(TestResource resource, bool validateId = false, bool skipValidate = false, bool skipSharedValidate = false) // NOSONAR
            {
                // nosemgrep: codacy.csharp.security.null-dereference
                base.ValidateResource(resource, validateId, skipValidate, skipSharedValidate);
            }
        }

        private TestController _controller;

        [SetUp]
        public void Setup()
        {
            _controller = new TestController();
        }

        [Test]
        public void validate_id_should_throw_for_zero()
        {
            _controller.Invoking(c => c.ValidateId(0))
                .Should().Throw<BadRequestException>()
                .WithMessage("*not a valid ID*");
        }

        [Test]
        public void validate_id_should_throw_for_negative_value()
        {
            _controller.Invoking(c => c.ValidateId(-1))
                .Should().Throw<BadRequestException>()
                .WithMessage("*not a valid ID*");
        }

        [Test]
        public void validate_id_should_not_throw_for_positive_value()
        {
            _controller.Invoking(c => c.ValidateId(1))
                .Should().NotThrow();
        }

        [Test]
        public void validate_id_should_not_throw_for_large_positive_value()
        {
            _controller.Invoking(c => c.ValidateId(999))
                .Should().NotThrow();
        }

        [Test]
        public void validate_resource_should_throw_for_null_resource()
        {
            _controller.Invoking(c => c.ValidateResource(null))
                .Should().Throw<BadRequestException>()
                .WithMessage("*can't be empty*");
        }

        [Test]
        public void validate_resource_should_not_throw_for_valid_resource()
        {
            var resource = new TestResource { Id = 1, Name = "Test" };

            _controller.Invoking(c => c.ValidateResource(resource))
                .Should().NotThrow();
        }

        [Test]
        public void resource_name_should_be_lowercase_without_resource_suffix()
        {
            var resource = new TestResource();

            resource.ResourceName.Should().Be("test");
        }

        [Test]
        public void resource_id_should_default_to_zero()
        {
            var resource = new TestResource();

            resource.Id.Should().Be(0);
        }
    }
}
