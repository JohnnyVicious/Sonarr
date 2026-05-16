using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class ApiTestDataFixture : IntegrationTest
    {
        [Test]
        public void should_create_core_api_records()
        {
            var rootFolder = TestData.RootFolder();
            var tag = TestData.Tag();
            var series = TestData.Series(tags: tag);
            var episodes = TestData.Episodes(series);
            var episode = episodes.First();
            var episodeFile = TestData.EpisodeFile(series, episode.SeasonNumber, episode.EpisodeNumber, Quality.SDTV);
            var qualityProfile = TestData.QualityProfile();

            rootFolder.Id.Should().NotBe(0);
            tag.Id.Should().NotBe(0);
            series.Id.Should().NotBe(0);
            series.Tags.Should().Contain(tag.Id);
            episodes.Should().NotBeEmpty();
            episodeFile.Id.Should().NotBe(0);
            qualityProfile.Id.Should().NotBe(0);
        }
    }
}
