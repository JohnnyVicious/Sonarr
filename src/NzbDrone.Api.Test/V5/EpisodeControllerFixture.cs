using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Episodes;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class EpisodeControllerFixture : TestBase<EpisodeController>
    {
        private NzbDrone.Core.Tv.Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Title = "Test Series",
                TvdbId = 12345,
                Seasons = new List<Season>(),
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                Tags = new HashSet<int>()
            };

            _episode = new Episode
            {
                Id = 10,
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Pilot",
                Monitored = true,
                Series = _series
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(1))
                .Returns(_series);
        }

        [Test]
        public void should_get_episodes_by_series_id()
        {
            var episodes = new List<Episode> { _episode };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodeBySeries(1))
                .Returns(episodes);

            var result = Subject.GetEpisodes(1, null, new List<int>(), null, Array.Empty<EpisodeSubresource>());

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<EpisodeResource>>>().Subject;
            okResult.Value.Should().HaveCount(1);
            okResult.Value[0].Title.Should().Be("Pilot");
            okResult.Value[0].SeriesId.Should().Be(1);
        }

        [Test]
        public void should_get_episodes_by_series_and_season()
        {
            var episodes = new List<Episode> { _episode };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(1, 1))
                .Returns(episodes);

            var result = Subject.GetEpisodes(1, 1, new List<int>(), null, Array.Empty<EpisodeSubresource>());

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<EpisodeResource>>>().Subject;
            okResult.Value.Should().HaveCount(1);
            okResult.Value[0].SeasonNumber.Should().Be(1);
        }

        [Test]
        public void should_get_episodes_by_episode_ids()
        {
            var episodes = new List<Episode> { _episode };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodes(It.Is<IEnumerable<int>>(ids => ids.Contains(10))))
                .Returns(episodes);

            var result = Subject.GetEpisodes(null, null, new List<int> { 10 }, null, Array.Empty<EpisodeSubresource>());

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<EpisodeResource>>>().Subject;
            okResult.Value.Should().HaveCount(1);
            okResult.Value[0].Id.Should().Be(10);
        }

        [Test]
        public void should_get_episodes_by_episode_file_id()
        {
            var episodes = new List<Episode> { _episode };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesByFileId(5))
                .Returns(episodes);

            var result = Subject.GetEpisodes(null, null, new List<int>(), 5, Array.Empty<EpisodeSubresource>());

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<EpisodeResource>>>().Subject;
            okResult.Value.Should().HaveCount(1);
        }

        [Test]
        public void should_throw_when_no_filter_provided()
        {
            Assert.Throws<Sonarr.Http.REST.BadRequestException>(() =>
                Subject.GetEpisodes(null, null, new List<int>(), null, Array.Empty<EpisodeSubresource>()));
        }

        [Test]
        public void should_set_episode_monitored()
        {
            var resource = new EpisodeResource { Id = 10, Monitored = false };

            var updatedEpisode = new Episode
            {
                Id = 10,
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Pilot",
                Monitored = false,
                Series = _series
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisode(10))
                .Returns(updatedEpisode);

            var result = Subject.SetEpisodeMonitored(10, resource);

            Mocker.GetMock<IEpisodeService>()
                .Verify(s => s.SetEpisodeMonitored(10, false), Times.Once());

            result.Value.Monitored.Should().BeFalse();
        }

        [Test]
        public void should_set_multiple_episodes_monitored()
        {
            var episodes = new List<Episode>
            {
                new Episode { Id = 10, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, Series = _series },
                new Episode { Id = 11, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true, Series = _series }
            };

            var monitoredResource = new EpisodesMonitoredResource
            {
                EpisodeIds = new List<int> { 10, 11 },
                Monitored = true
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                .Returns(episodes);

            var result = Subject.SetEpisodesMonitored(monitoredResource, Array.Empty<EpisodeSubresource>());

            Mocker.GetMock<IEpisodeService>()
                .Verify(s => s.SetMonitored(It.Is<IEnumerable<int>>(ids => ids.Count() == 2), true), Times.Once());

            result.Value.Should().HaveCount(2);
        }

        [Test]
        public void should_set_single_episode_monitored_via_bulk_endpoint()
        {
            var episode = new Episode
            {
                Id = 10,
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Monitored = false,
                Series = _series
            };

            var monitoredResource = new EpisodesMonitoredResource
            {
                EpisodeIds = new List<int> { 10 },
                Monitored = false
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<Episode> { episode });

            var result = Subject.SetEpisodesMonitored(monitoredResource, Array.Empty<EpisodeSubresource>());

            Mocker.GetMock<IEpisodeService>()
                .Verify(s => s.SetEpisodeMonitored(10, false), Times.Once());
        }
    }
}
