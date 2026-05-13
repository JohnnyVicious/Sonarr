using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Health;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class HealthControllerFixture : TestBase<HealthController>
    {
        [Test]
        public void should_return_health_check_results()
        {
            var healthChecks = new List<HealthCheck>
            {
                new HealthCheck(typeof(HealthCheckService), HealthCheckResult.Warning, HealthCheckReason.AppDataLocation, "Test warning", "test"),
                new HealthCheck(typeof(HealthCheckService), HealthCheckResult.Error, HealthCheckReason.AppDataLocation, "Test error", "error")
            };

            Mocker.GetMock<IHealthCheckService>()
                .Setup(s => s.Results())
                .Returns(healthChecks);

            var result = Subject.GetHealth();

            result.Value.Should().HaveCount(2);
            result.Value[0].Type.Should().Be(HealthCheckResult.Warning);
            result.Value[0].Message.Should().Be("Test warning");
            result.Value[1].Type.Should().Be(HealthCheckResult.Error);
            result.Value[1].Message.Should().Be("Test error");
        }

        [Test]
        public void should_return_empty_list_when_no_health_issues()
        {
            Mocker.GetMock<IHealthCheckService>()
                .Setup(s => s.Results())
                .Returns(new List<HealthCheck>());

            var result = Subject.GetHealth();

            result.Value.Should().BeEmpty();
        }

        [Test]
        public void should_return_correct_source_name()
        {
            var healthChecks = new List<HealthCheck>
            {
                new HealthCheck(typeof(HealthCheckService), HealthCheckResult.Warning, HealthCheckReason.AppDataLocation, "Msg", "url")
            };

            Mocker.GetMock<IHealthCheckService>()
                .Setup(s => s.Results())
                .Returns(healthChecks);

            var result = Subject.GetHealth();

            result.Value.Should().HaveCount(1);
            result.Value[0].Source.Should().Be(nameof(HealthCheckService));
        }

        [Test]
        public void should_map_wiki_url()
        {
            var healthChecks = new List<HealthCheck>
            {
                new HealthCheck(typeof(HealthCheckService), HealthCheckResult.Error, HealthCheckReason.AppDataLocation, "Disk full", "disk-full")
            };

            Mocker.GetMock<IHealthCheckService>()
                .Setup(s => s.Results())
                .Returns(healthChecks);

            var result = Subject.GetHealth();

            result.Value[0].WikiUrl.Should().Contain("disk-full");
        }

        [Test]
        public void should_map_health_check_reason()
        {
            var healthChecks = new List<HealthCheck>
            {
                new HealthCheck(typeof(HealthCheckService), HealthCheckResult.Warning, HealthCheckReason.UpdateAvailable, "Update available", "update")
            };

            Mocker.GetMock<IHealthCheckService>()
                .Setup(s => s.Results())
                .Returns(healthChecks);

            var result = Subject.GetHealth();

            result.Value[0].Reason.Should().Be(HealthCheckReason.UpdateAvailable);
        }

        [Test]
        public void get_resource_by_id_should_throw_not_implemented()
        {
            Assert.Throws<NotImplementedException>(() => Subject.GetResourceByIdWithErrorHandler(1));
        }
    }
}
