using System;
using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class ApiTestDataInfrastructureFixture
    {
        [Test]
        public void should_generate_deterministic_scoped_names()
        {
            var names = new ApiTestDataNames("NzbDrone.Integration.Test.ApiTests.SeriesFixture.should create data");

            names.Next("Root Folder").Should().Be("api-root-folder-nzbdrone-integration-test-apitests-seriesfixture-01");
            names.Next("Root Folder").Should().Be("api-root-folder-nzbdrone-integration-test-apitests-seriesfixture-02");
        }

        [Test]
        public void should_sanitize_empty_or_symbol_only_names()
        {
            var names = new ApiTestDataNames("!!!");

            names.Next("@@@").Should().Be("api-data-data-01");
        }

        [Test]
        public void should_reject_unsafe_path_segments()
        {
            ApiTestData.SafePathSegment("Series.Title.S01E01.mkv", "fileName")
                .Should().Be("Series.Title.S01E01.mkv");

            var rooted = () => ApiTestData.SafePathSegment("/tmp/escape", "fileName");
            var nested = () => ApiTestData.SafePathSegment("nested/path", "fileName");

            rooted.Should().Throw<ArgumentException>();
            nested.Should().Throw<ArgumentException>();
        }
    }
}
