using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class EpisodeServiceFixture : CoreTest<EpisodeService>
    {
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _episodes = Builder<Episode>.CreateListOfSize(5)
                .All()
                .With(e => e.SeriesId = 1)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .BuildList();
        }

        [Test]
        public void should_get_episode_by_id()
        {
            var episode = _episodes.First();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.Get(episode.Id))
                  .Returns(episode);

            Subject.GetEpisode(episode.Id).Should().Be(episode);
        }

        [Test]
        public void should_get_episodes_by_ids()
        {
            var ids = _episodes.Select(e => e.Id).ToList();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.Get(ids))
                  .Returns(_episodes);

            Subject.GetEpisodes(ids).Should().HaveCount(5);
        }

        [Test]
        public void should_get_episodes_by_series()
        {
            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.GetEpisodes(1))
                  .Returns(_episodes);

            Subject.GetEpisodeBySeries(1).Should().HaveCount(5);
        }

        [Test]
        public void should_get_episodes_by_file_id()
        {
            var episodesForFile = new List<Episode> { _episodes.First() };

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.GetEpisodeByFileId(10))
                  .Returns(episodesForFile);

            Subject.GetEpisodesByFileId(10).Should().HaveCount(1);
        }

        [Test]
        public void should_set_monitored_for_multiple_episodes()
        {
            var ids = new List<int> { 1, 2, 3 };

            Subject.SetMonitored(ids, true);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.SetMonitored(ids, true), Times.Once());
        }

        [Test]
        public void should_set_episode_monitored()
        {
            var episode = _episodes.First();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.Get(episode.Id))
                  .Returns(episode);

            Subject.SetEpisodeMonitored(episode.Id, false);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.SetMonitoredFlat(episode, false), Times.Once());
        }

        [Test]
        public void should_update_many_episodes()
        {
            Subject.UpdateMany(_episodes);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.UpdateMany(_episodes), Times.Once());
        }

        [Test]
        public void should_insert_many_episodes()
        {
            Subject.InsertMany(_episodes);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.InsertMany(_episodes), Times.Once());
        }

        [Test]
        public void should_delete_many_episodes()
        {
            Subject.DeleteMany(_episodes);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.DeleteMany(_episodes), Times.Once());
        }

        [Test]
        public void should_update_episode()
        {
            var episode = _episodes.First();

            Subject.UpdateEpisode(episode);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.Update(episode), Times.Once());
        }

        [Test]
        public void should_get_episodes_by_season()
        {
            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.GetEpisodes(1, 1))
                  .Returns(_episodes);

            Subject.GetEpisodesBySeason(1, 1).Should().HaveCount(5);
        }

        [Test]
        public void should_get_episodes_between_dates()
        {
            var start = DateTime.UtcNow.AddDays(-7);
            var end = DateTime.UtcNow;

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.EpisodesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), true, false))
                  .Returns(_episodes);

            Subject.EpisodesBetweenDates(start, end, true, false).Should().HaveCount(5);
        }

        [Test]
        public void should_find_episode_by_series_season_episode()
        {
            var episode = _episodes.First();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.Find(1, 1, 1))
                  .Returns(episode);

            Subject.FindEpisode(1, 1, 1).Should().Be(episode);
        }

        [Test]
        public void should_handle_series_deleted_event()
        {
            var series = Builder<Series>.CreateListOfSize(1)
                .All()
                .With(s => s.Id = 1)
                .BuildList();

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.GetEpisodesBySeriesIds(It.IsAny<List<int>>()))
                  .Returns(_episodes);

            Subject.HandleAsync(new SeriesDeletedEvent(series, false, false));

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.DeleteMany(_episodes), Times.Once());
        }

        [Test]
        public void should_set_episode_monitored_by_season()
        {
            Subject.SetEpisodeMonitoredBySeason(1, 2, true);

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.SetMonitoredBySeason(1, 2, true), Times.Once());
        }

        [Test]
        public void should_handle_episode_file_deleted_and_clear_file_id()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Id = 10)
                .Build();

            var episodes = new List<Episode> { _episodes.First() };

            Mocker.GetMock<IEpisodeRepository>()
                  .Setup(s => s.GetEpisodeByFileId(10))
                  .Returns(episodes);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.AutoUnmonitorPreviouslyDownloadedEpisodes)
                  .Returns(false);

            Subject.Handle(new EpisodeFileDeletedEvent(episodeFile, DeleteMediaFileReason.Upgrade));

            Mocker.GetMock<IEpisodeRepository>()
                  .Verify(v => v.ClearFileId(episodes.First(), false), Times.Once());
        }
    }
}
