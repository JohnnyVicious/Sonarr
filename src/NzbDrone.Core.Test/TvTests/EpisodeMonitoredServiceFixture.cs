using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class EpisodeMonitoredServiceFixture : CoreTest<EpisodeMonitoredService>
    {
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.Title = "Test Series")
                .With(s => s.Status = SeriesStatusType.Continuing)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 2, Monitored = true },
                    new Season { SeasonNumber = 3, Monitored = true }
                })
                .Build();

            _episodes = new List<Episode>
            {
                // Season 1, episode 1, has file, aired
                new Episode { Id = 1, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 1, EpisodeFileId = 1, Monitored = true, AirDateUtc = DateTime.UtcNow.AddDays(-90) },
                // Season 1, episode 2, no file, aired
                new Episode { Id = 2, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 2, EpisodeFileId = 0, Monitored = true, AirDateUtc = DateTime.UtcNow.AddDays(-80) },
                // Season 2, episode 1, has file, aired
                new Episode { Id = 3, SeriesId = 1, SeasonNumber = 2, EpisodeNumber = 1, EpisodeFileId = 2, Monitored = true, AirDateUtc = DateTime.UtcNow.AddDays(-30) },
                // Season 2, episode 2, no file, not aired
                new Episode { Id = 4, SeriesId = 1, SeasonNumber = 2, EpisodeNumber = 2, EpisodeFileId = 0, Monitored = true, AirDateUtc = DateTime.UtcNow.AddDays(30) },
                // Season 3, episode 1, no file, future
                new Episode { Id = 5, SeriesId = 1, SeasonNumber = 3, EpisodeNumber = 1, EpisodeFileId = 0, Monitored = true, AirDateUtc = DateTime.UtcNow.AddDays(60) },
                // Season 3, episode 2, no file, no air date (TBA)
                new Episode { Id = 6, SeriesId = 1, SeasonNumber = 3, EpisodeNumber = 2, EpisodeFileId = 0, Monitored = true, AirDateUtc = null },
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(_episodes);
        }

        [Test]
        public void should_update_series_without_touching_episodes_when_monitoring_options_null()
        {
            Subject.SetEpisodeMonitoredStatus(_series, null);

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(_series, false), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.GetEpisodeBySeries(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_monitor_all_episodes_when_monitor_all()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.All };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            _episodes.Where(e => e.SeasonNumber > 0).Should().OnlyContain(e => e.Monitored);
        }

        [Test]
        public void should_monitor_none_when_monitor_none()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.None };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            _episodes.Should().OnlyContain(e => !e.Monitored);
        }

        [Test]
        public void should_monitor_future_episodes_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Future };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            // Aired episodes should not be monitored
            _episodes.Single(e => e.Id == 1).Monitored.Should().BeFalse();
            _episodes.Single(e => e.Id == 2).Monitored.Should().BeFalse();
            _episodes.Single(e => e.Id == 3).Monitored.Should().BeFalse();

            // Future and TBA episodes should be monitored
            _episodes.Single(e => e.Id == 4).Monitored.Should().BeTrue();
            _episodes.Single(e => e.Id == 5).Monitored.Should().BeTrue();
            _episodes.Single(e => e.Id == 6).Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_missing_episodes_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Missing };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            // Episodes with files should not be monitored
            _episodes.Single(e => e.Id == 1).Monitored.Should().BeFalse();
            _episodes.Single(e => e.Id == 3).Monitored.Should().BeFalse();

            // Episodes without files should be monitored
            _episodes.Single(e => e.Id == 2).Monitored.Should().BeTrue();
            _episodes.Single(e => e.Id == 4).Monitored.Should().BeTrue();
            _episodes.Single(e => e.Id == 5).Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_existing_episodes_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Existing };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            // Episodes with files should be monitored
            _episodes.Single(e => e.Id == 1).Monitored.Should().BeTrue();
            _episodes.Single(e => e.Id == 3).Monitored.Should().BeTrue();

            // Episodes without files should not be monitored
            _episodes.Single(e => e.Id == 2).Monitored.Should().BeFalse();
            _episodes.Single(e => e.Id == 4).Monitored.Should().BeFalse();
        }

        [Test]
        public void should_monitor_first_season_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.FirstSeason };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            _episodes.Where(e => e.SeasonNumber == 1).Should().OnlyContain(e => e.Monitored);
            _episodes.Where(e => e.SeasonNumber > 1).Should().OnlyContain(e => !e.Monitored);
        }

        [Test]
        public void should_monitor_last_season_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.LastSeason };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            _episodes.Where(e => e.SeasonNumber == 3).Should().OnlyContain(e => e.Monitored);
            _episodes.Where(e => e.SeasonNumber < 3).Should().OnlyContain(e => !e.Monitored);
        }

        [Test]
        public void should_monitor_pilot_episode_only()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Pilot };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            _episodes.Single(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1).Monitored.Should().BeTrue();
            _episodes.Where(e => !(e.SeasonNumber == 1 && e.EpisodeNumber == 1)).Should().OnlyContain(e => !e.Monitored);
        }

        [Test]
        public void should_skip_episode_monitoring_when_skip()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Skip };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.GetEpisodeBySeries(It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.IsAny<List<Episode>>()), Times.Never());
        }

        [Test]
        public void should_use_legacy_path_for_unknown_monitor_type()
        {
            var options = new MonitoringOptions
            {
                Monitor = MonitorTypes.Unknown,
                IgnoreEpisodesWithFiles = true,
                IgnoreEpisodesWithoutFiles = false
            };

            Subject.SetEpisodeMonitoredStatus(_series, options);

            // Episodes with files should be unmonitored
            _episodes.Single(e => e.Id == 1).Monitored.Should().BeFalse();
            _episodes.Single(e => e.Id == 3).Monitored.Should().BeFalse();
        }
    }
}
