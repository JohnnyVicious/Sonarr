using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearch
{
    [TestFixture]
    public class EpisodeSearchServiceFixture : CoreTest<EpisodeSearchService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns(Task.FromResult(new ProcessedDecisions(
                      new List<DownloadDecision>(),
                      new List<DownloadDecision>(),
                      new List<DownloadDecision>())));

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>());
        }

        [Test]
        public void should_search_for_each_episode_id()
        {
            var command = new EpisodeSearchCommand
            {
                EpisodeIds = new List<int> { 1, 2, 3 },
                Trigger = CommandTrigger.Manual
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<int>(), true, false))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Subject.Execute(command);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(It.IsAny<int>(), true, false), Times.Exactly(3));
        }

        [Test]
        public void should_search_for_single_episode()
        {
            var command = new EpisodeSearchCommand
            {
                EpisodeIds = new List<int> { 1 },
                Trigger = CommandTrigger.Manual
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(1, true, false))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Subject.Execute(command);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(1, true, false), Times.Once());
        }

        [Test]
        public void should_process_download_decisions_for_episode_search()
        {
            var decisions = new List<DownloadDecision>
            {
                new DownloadDecision(new RemoteEpisode())
            };

            var command = new EpisodeSearchCommand
            {
                EpisodeIds = new List<int> { 1 },
                Trigger = CommandTrigger.Manual
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(1, true, false))
                  .Returns(Task.FromResult(decisions));

            Subject.Execute(command);

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Verify(v => v.ProcessDecisions(decisions), Times.Once());
        }

        [Test]
        public void missing_search_should_filter_queued_episodes()
        {
            var episodes = Builder<Episode>.CreateListOfSize(3)
                .All()
                .With(e => e.Monitored = true)
                .With(e => e.HasFile = false)
                .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-7))
                .Build()
                .ToList();

            episodes[0].Id = 1;
            episodes[1].Id = 2;
            episodes[2].Id = 3;

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(episodes);

            var queueItem = new Queue.Queue
            {
                Episodes = new List<Episode> { new Episode { Id = 2 } }
            };

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue> { queueItem });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            var command = new MissingEpisodeSearchCommand { SeriesId = 1, Trigger = CommandTrigger.Manual };

            Subject.Execute(command);

            // Episode 2 is queued, so only 2 episodes should be searched
            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void missing_search_with_series_id_should_get_episodes_for_series()
        {
            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeriesId = 5, Monitored = true, HasFile = false, AirDateUtc = DateTime.UtcNow.AddDays(-7), SeasonNumber = 1, EpisodeNumber = 1 }
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(5))
                  .Returns(episodes);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            var command = new MissingEpisodeSearchCommand { SeriesId = 5, Trigger = CommandTrigger.Manual };

            Subject.Execute(command);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.GetEpisodeBySeries(5), Times.Once());
        }

        [Test]
        public void missing_search_should_not_search_unaired_episodes()
        {
            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeriesId = 1, Monitored = true, HasFile = false, AirDateUtc = DateTime.UtcNow.AddDays(-7), SeasonNumber = 1, EpisodeNumber = 1 },
                new Episode { Id = 2, SeriesId = 1, Monitored = true, HasFile = false, AirDateUtc = DateTime.UtcNow.AddDays(7), SeasonNumber = 1, EpisodeNumber = 2 }
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(1))
                  .Returns(episodes);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            var command = new MissingEpisodeSearchCommand { SeriesId = 1, Trigger = CommandTrigger.Manual };

            Subject.Execute(command);

            // Only 1 episode should be searched (the aired one)
            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void missing_search_should_not_include_episodes_with_files()
        {
            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeriesId = 1, Monitored = true, HasFile = true, AirDateUtc = DateTime.UtcNow.AddDays(-7), SeasonNumber = 1, EpisodeNumber = 1 },
                new Episode { Id = 2, SeriesId = 1, Monitored = true, HasFile = false, AirDateUtc = DateTime.UtcNow.AddDays(-3), SeasonNumber = 1, EpisodeNumber = 2 }
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(1))
                  .Returns(episodes);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            var command = new MissingEpisodeSearchCommand { SeriesId = 1, Trigger = CommandTrigger.Manual };

            Subject.Execute(command);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_set_user_invoked_true_for_manual_trigger()
        {
            var command = new EpisodeSearchCommand
            {
                EpisodeIds = new List<int> { 1 },
                Trigger = CommandTrigger.Manual
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(1, true, false))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Subject.Execute(command);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(1, true, false), Times.Once());
        }
    }
}
