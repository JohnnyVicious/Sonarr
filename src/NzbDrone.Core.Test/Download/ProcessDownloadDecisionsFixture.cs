using System.Collections.Generic; // NOSONAR
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class ProcessDownloadDecisionsFixture : CoreTest<ProcessDownloadDecisions>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            _remoteEpisode = new RemoteEpisode
            {
                Series = Builder<Series>.CreateNew().Build(),
                Episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.Id = 1).Build() },
                ParsedEpisodeInfo = new ParsedEpisodeInfo(),
                Release = new ReleaseInfo
                {
                    DownloadProtocol = DownloadProtocol.Usenet,
                    Indexer = "TestIndexer",
                    IndexerPriority = 25
                }
            };

            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(v => v.PrioritizeDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns<List<DownloadDecision>>(v => v);
        }

        [Test]
        public async Task should_return_empty_when_no_decisions()
        {
            var decisions = new List<DownloadDecision>();

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().BeEmpty();
            result.Pending.Should().BeEmpty();
            result.Rejected.Should().BeEmpty();
        }

        [Test]
        public async Task should_grab_approved_decision()
        {
            var decision = new DownloadDecision(_remoteEpisode);
            var decisions = new List<DownloadDecision> { decision };

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().HaveCount(1);
            Mocker.GetMock<IDownloadService>()
                  .Verify(v => v.DownloadReport(It.IsAny<RemoteEpisode>(), null), Times.Once());
        }

        [Test]
        public async Task should_add_rejected_decisions_to_rejected_list()
        {
            var rejection = new DownloadRejection(DownloadRejectionReason.Unknown, "Test Rejection", RejectionType.Permanent);
            var decision = new DownloadDecision(_remoteEpisode, rejection);
            var decisions = new List<DownloadDecision> { decision };

            var result = await Subject.ProcessDecisions(decisions);

            result.Rejected.Should().HaveCount(1);
            result.Grabbed.Should().BeEmpty();
        }

        [Test]
        public async Task should_add_temporarily_rejected_to_pending()
        {
            var rejection = new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Delay", RejectionType.Temporary);
            var decision = new DownloadDecision(_remoteEpisode, rejection);
            var decisions = new List<DownloadDecision> { decision };

            var result = await Subject.ProcessDecisions(decisions);

            result.Pending.Should().HaveCount(1);
            result.Grabbed.Should().BeEmpty();
        }

        [Test]
        public async Task should_not_grab_episode_already_grabbed()
        {
            var decision1 = new DownloadDecision(_remoteEpisode);
            var decision2 = new DownloadDecision(_remoteEpisode);
            var decisions = new List<DownloadDecision> { decision1, decision2 };

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().HaveCount(1);
            Mocker.GetMock<IDownloadService>()
                  .Verify(v => v.DownloadReport(It.IsAny<RemoteEpisode>(), null), Times.Once());
        }

        [Test]
        public async Task process_single_decision_should_return_skipped_for_null()
        {
            var result = await Subject.ProcessDecision(null, null);

            result.Should().Be(ProcessedDecisionResult.Skipped);
        }

        [Test]
        public async Task process_single_decision_should_return_rejected_for_unqualified()
        {
            var rejection = new DownloadRejection(DownloadRejectionReason.Unknown, "Permanent", RejectionType.Permanent);
            var decision = new DownloadDecision(_remoteEpisode, rejection);

            var result = await Subject.ProcessDecision(decision, null);

            result.Should().Be(ProcessedDecisionResult.Rejected);
        }

        [Test]
        public async Task process_single_decision_should_return_pending_for_temporarily_rejected()
        {
            var rejection = new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Delay", RejectionType.Temporary);
            var decision = new DownloadDecision(_remoteEpisode, rejection);

            var result = await Subject.ProcessDecision(decision, null);

            result.Should().Be(ProcessedDecisionResult.Pending);

            Mocker.GetMock<IPendingReleaseService>()
                  .Verify(v => v.Add(decision, PendingReleaseReason.Delay), Times.Once());
        }

        [Test]
        public async Task process_single_decision_should_grab_approved()
        {
            var decision = new DownloadDecision(_remoteEpisode);

            var result = await Subject.ProcessDecision(decision, null);

            result.Should().Be(ProcessedDecisionResult.Grabbed);
        }

        [Test]
        public async Task should_not_grab_rejected_without_episodes()
        {
            var remoteEpisode = new RemoteEpisode
            {
                Series = Builder<Series>.CreateNew().Build(),
                Episodes = new List<Episode>(),
                ParsedEpisodeInfo = new ParsedEpisodeInfo(),
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            };

            var decision = new DownloadDecision(remoteEpisode);
            var decisions = new List<DownloadDecision> { decision };

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().BeEmpty();
        }
    }
}
