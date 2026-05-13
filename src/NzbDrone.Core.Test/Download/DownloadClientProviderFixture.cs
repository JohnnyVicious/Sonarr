using System.Collections.Generic; // NOSONAR
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class DownloadClientProviderFixture : CoreTest<DownloadClientProvider>
    {
        private List<IDownloadClient> _downloadClients;

        [SetUp]
        public void Setup()
        {
            _downloadClients = new List<IDownloadClient>();

            Mocker.GetMock<IDownloadClientFactory>()
                  .Setup(s => s.GetAvailableProviders())
                  .Returns(_downloadClients);

            Mocker.GetMock<IDownloadClientStatusService>()
                  .Setup(s => s.GetBlockedProviders())
                  .Returns(new List<DownloadClientStatus>());
        }

        private Mock<IDownloadClient> WithClient(int id, DownloadProtocol protocol = DownloadProtocol.Usenet, int priority = 1)
        {
            var mock = new Mock<IDownloadClient>();
            // nosemgrep: codacy.csharp.security.null-dereference
            mock.SetupGet(c => c.Protocol).Returns(protocol);
            mock.SetupGet(c => c.Definition).Returns(new DownloadClientDefinition
            {
                Id = id,
                Name = "Client" + id,
                Priority = priority,
                Tags = new HashSet<int>()
            });

            _downloadClients.Add(mock.Object);

            return mock;
        }

        [Test]
        public void should_return_null_when_no_clients_available()
        {
            Subject.GetDownloadClient(DownloadProtocol.Usenet).Should().BeNull();
        }

        [Test]
        public void should_return_usenet_client_when_usenet_requested()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Torrent);

            var result = Subject.GetDownloadClient(DownloadProtocol.Usenet);

            result.Should().NotBeNull();
            result.Protocol.Should().Be(DownloadProtocol.Usenet);
        }

        [Test]
        public void should_return_torrent_client_when_torrent_requested()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Torrent);

            var result = Subject.GetDownloadClient(DownloadProtocol.Torrent);

            result.Should().NotBeNull();
            result.Protocol.Should().Be(DownloadProtocol.Torrent);
        }

        [Test]
        public void should_prefer_higher_priority_client()
        {
            WithClient(1, DownloadProtocol.Usenet, priority: 2);
            WithClient(2, DownloadProtocol.Usenet, priority: 1);

            var result = Subject.GetDownloadClient(DownloadProtocol.Usenet);

            result.Definition.Id.Should().Be(2);
        }

        [Test]
        public void should_get_all_download_clients()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Torrent);

            var result = Subject.GetDownloadClients();

            result.Should().HaveCount(2);
        }

        [Test]
        public void should_filter_blocked_clients()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Usenet);

            Mocker.GetMock<IDownloadClientStatusService>()
                  .Setup(s => s.GetBlockedProviders())
                  .Returns(new List<DownloadClientStatus>
                  {
                      new DownloadClientStatus { ProviderId = 1, DisabledTill = System.DateTimeOffset.UtcNow.AddMinutes(5) }
                  });

            var result = Subject.GetDownloadClients(filterBlockedClients: true).ToList();

            result.Should().HaveCount(1);
            result.First().Definition.Id.Should().Be(2);
        }

        [Test]
        public void should_get_client_by_id()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Torrent);

            var result = Subject.Get(2);

            result.Definition.Id.Should().Be(2);
        }

        [Test]
        public void should_round_robin_between_same_priority_clients()
        {
            WithClient(1, DownloadProtocol.Usenet, priority: 1);
            WithClient(2, DownloadProtocol.Usenet, priority: 1);

            var first = Subject.GetDownloadClient(DownloadProtocol.Usenet);
            var second = Subject.GetDownloadClient(DownloadProtocol.Usenet);

            first.Definition.Id.Should().Be(1);
            second.Definition.Id.Should().Be(2);
        }

        [Test]
        public void should_use_indexer_specific_download_client_when_configured()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Usenet);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.Find(5))
                  .Returns(new IndexerDefinition
                  {
                      Id = 5,
                      Name = "TestIndexer",
                      DownloadClientId = 2
                  });

            var result = Subject.GetDownloadClient(DownloadProtocol.Usenet, indexerId: 5);

            result.Definition.Id.Should().Be(2);
        }

        [Test]
        public void should_throw_when_indexer_specific_client_not_found()
        {
            WithClient(1, DownloadProtocol.Usenet);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.Find(5))
                  .Returns(new IndexerDefinition
                  {
                      Id = 5,
                      Name = "TestIndexer",
                      DownloadClientId = 99
                  });

            Assert.Throws<DownloadClientUnavailableException>(() =>
                Subject.GetDownloadClient(DownloadProtocol.Usenet, indexerId: 5));
        }

        [Test]
        public void should_report_successful_download_client()
        {
            WithClient(1, DownloadProtocol.Usenet);
            WithClient(2, DownloadProtocol.Usenet);

            Subject.ReportSuccessfulDownloadClient(DownloadProtocol.Usenet, 1);

            var result = Subject.GetDownloadClient(DownloadProtocol.Usenet);

            result.Definition.Id.Should().Be(2);
        }
    }
}
