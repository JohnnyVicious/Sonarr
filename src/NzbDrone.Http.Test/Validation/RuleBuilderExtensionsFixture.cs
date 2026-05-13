using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Sonarr.Http.Validation;

namespace NzbDrone.Http.Test.Validation
{
    [TestFixture]
    public class RuleBuilderExtensionsFixture : TestBase
    {
        public class TestResource
        {
            public int Id { get; set; }
            public string Url { get; set; }
            public string Name { get; set; }
            public int RssSyncInterval { get; set; }
        }

        public class ValidIdValidator : AbstractValidator<TestResource>
        {
            public ValidIdValidator()
            {
                RuleFor(r => r.Id).ValidId();
            }
        }

        public class IsZeroValidator : AbstractValidator<TestResource>
        {
            public IsZeroValidator()
            {
                RuleFor(r => r.Id).IsZero();
            }
        }

        public class HttpProtocolValidator : AbstractValidator<TestResource>
        {
            public HttpProtocolValidator()
            {
                RuleFor(r => r.Url).HaveHttpProtocol();
            }
        }

        public class NotBlankValidator : AbstractValidator<TestResource>
        {
            public NotBlankValidator()
            {
                RuleFor(r => r.Name).NotBlank();
            }
        }

        public class RssSyncValidator : AbstractValidator<TestResource>
        {
            public RssSyncValidator()
            {
                RuleFor(r => r.RssSyncInterval).IsValidRssSyncInterval();
            }
        }

        [Test]
        public void valid_id_should_pass_for_positive_value()
        {
            var validator = new ValidIdValidator();
            var resource = new TestResource { Id = 1 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_id_should_fail_for_zero()
        {
            var validator = new ValidIdValidator();
            var resource = new TestResource { Id = 0 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void valid_id_should_fail_for_negative_value()
        {
            var validator = new ValidIdValidator();
            var resource = new TestResource { Id = -1 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void is_zero_should_pass_for_zero()
        {
            var validator = new IsZeroValidator();
            var resource = new TestResource { Id = 0 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void is_zero_should_fail_for_nonzero()
        {
            var validator = new IsZeroValidator();
            var resource = new TestResource { Id = 5 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void http_protocol_should_pass_for_http_url()
        {
            var validator = new HttpProtocolValidator();
            var resource = new TestResource { Url = "http://localhost:8989" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void http_protocol_should_pass_for_https_url()
        {
            var validator = new HttpProtocolValidator();
            var resource = new TestResource { Url = "https://sonarr.example.com" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void http_protocol_should_fail_for_ftp_url()
        {
            var validator = new HttpProtocolValidator();
            var resource = new TestResource { Url = "ftp://files.example.com" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void http_protocol_should_fail_for_no_protocol()
        {
            var validator = new HttpProtocolValidator();
            var resource = new TestResource { Url = "localhost:8989" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void not_blank_should_pass_for_non_empty_string()
        {
            var validator = new NotBlankValidator();
            var resource = new TestResource { Name = "Sonarr" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void not_blank_should_fail_for_null()
        {
            var validator = new NotBlankValidator();
            var resource = new TestResource { Name = null };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void not_blank_should_fail_for_empty_string()
        {
            var validator = new NotBlankValidator();
            var resource = new TestResource { Name = "" };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void rss_sync_interval_should_pass_for_zero()
        {
            var validator = new RssSyncValidator();
            var resource = new TestResource { RssSyncInterval = 0 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [TestCase(10)]
        [TestCase(60)]
        [TestCase(120)]
        public void rss_sync_interval_should_pass_for_valid_values(int interval)
        {
            var validator = new RssSyncValidator();
            var resource = new TestResource { RssSyncInterval = interval };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeTrue();
        }

        [TestCase(5)]
        [TestCase(9)]
        public void rss_sync_interval_should_fail_for_values_below_ten(int interval)
        {
            var validator = new RssSyncValidator();
            var resource = new TestResource { RssSyncInterval = interval };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void rss_sync_interval_should_fail_for_values_above_120()
        {
            var validator = new RssSyncValidator();
            var resource = new TestResource { RssSyncInterval = 121 };

            var result = validator.Validate(resource);

            result.IsValid.Should().BeFalse();
        }
    }
}
