using System;
using FluentAssertions;
using NUnit.Framework;
using Sonarr.Api.V3.Queue;

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
            var currentDirectory = () => ApiTestData.SafePathSegment(".", "fileName");
            var parentDirectory = () => ApiTestData.SafePathSegment("..", "fileName");

            rooted.Should().Throw<ArgumentException>();
            nested.Should().Throw<ArgumentException>();
            currentDirectory.Should().Throw<ArgumentException>();
            parentDirectory.Should().Throw<ArgumentException>();
        }

        [Test]
        public void should_match_queued_download_with_unique_path_or_client_title_pair()
        {
            var filePath = "/tmp/watch/Series.Title.S01E01.mkv";
            var fileName = "Series.Title.S01E01.mkv";
            var downloadClient = "api-usenet-blackhole-test-01";

            ApiTestData.IsQueuedDownload(new QueueResource { OutputPath = filePath }, filePath, fileName, downloadClient)
                .Should().BeTrue();

            ApiTestData.IsQueuedDownload(
                new QueueResource { DownloadClient = downloadClient, Title = fileName },
                filePath,
                fileName,
                downloadClient).Should().BeTrue();

            ApiTestData.IsQueuedDownload(
                new QueueResource { DownloadClient = "other-client", Title = fileName },
                filePath,
                fileName,
                downloadClient).Should().BeFalse();

            ApiTestData.IsQueuedDownload(
                new QueueResource { DownloadClient = downloadClient, Title = "Other.Series.Title.S01E01.mkv" },
                filePath,
                fileName,
                downloadClient).Should().BeFalse();
        }
    }
}
