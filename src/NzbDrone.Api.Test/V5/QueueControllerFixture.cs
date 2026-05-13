using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Queue;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class QueueControllerFixture : TestBase<QueueController>
    {
        [SetUp]
        public void Setup()
        {
            var defaultProfile = new QualityProfile
            {
                Id = 1,
                Name = "Default",
                Cutoff = Quality.HDTV720p.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem { Quality = Quality.SDTV, Allowed = true },
                    new QualityProfileQualityItem { Quality = Quality.HDTV720p, Allowed = true },
                    new QualityProfileQualityItem { Quality = Quality.HDTV1080p, Allowed = true }
                },
                FormatItems = new List<ProfileFormatItem>()
            };

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.GetDefaultProfile(string.Empty))
                .Returns(defaultProfile);

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(new List<NzbDrone.Core.Queue.Queue>());

            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.GetPendingQueue())
                .Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        [Test]
        public void should_remove_pending_release()
        {
            var pendingItem = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Title = "Test Release",
                Languages = new List<Language> { Language.English },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.FindPendingQueueItem(1))
                .Returns(pendingItem);

            var result = Subject.RemoveAction(1);

            Mocker.GetMock<IPendingReleaseService>()
                .Verify(s => s.RemovePendingQueueItems(1), Times.Once());
        }

        [Test]
        public void should_throw_not_found_when_queue_item_not_found()
        {
            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.FindPendingQueueItem(999))
                .Returns((NzbDrone.Core.Queue.Queue)null);

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.Find(999))
                .Returns((NzbDrone.Core.Queue.Queue)null);

            Assert.Throws<Sonarr.Http.REST.NotFoundException>(() =>
                Subject.RemoveAction(999));
        }

        [Test]
        public void should_remove_tracked_download()
        {
            var queueItem = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                DownloadId = "abc123",
                Languages = new List<Language> { Language.English },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            var trackedDownload = new TrackedDownload
            {
                DownloadClient = 1,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "abc123",
                    Title = "Test Download"
                }
            };

            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.FindPendingQueueItem(1))
                .Returns((NzbDrone.Core.Queue.Queue)null);

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.Find(1))
                .Returns(queueItem);

            Mocker.GetMock<ITrackedDownloadService>()
                .Setup(s => s.Find("abc123"))
                .Returns(trackedDownload);

            Mocker.GetMock<IIgnoredDownloadService>()
                .Setup(s => s.IgnoreDownload(It.IsAny<TrackedDownload>()))
                .Returns(true);

            Subject.RemoveAction(1, removeFromClient: false, blocklist: false);

            Mocker.GetMock<ITrackedDownloadService>()
                .Verify(s => s.StopTracking("abc123"), Times.Once());
        }

        [Test]
        public void should_remove_many_pending_releases()
        {
            var pendingItem1 = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Title = "Release 1",
                Languages = new List<Language> { Language.English },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            var pendingItem2 = new NzbDrone.Core.Queue.Queue
            {
                Id = 2,
                Title = "Release 2",
                Languages = new List<Language> { Language.English },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.FindPendingQueueItem(1))
                .Returns(pendingItem1);

            Mocker.GetMock<IPendingReleaseService>()
                .Setup(s => s.FindPendingQueueItem(2))
                .Returns(pendingItem2);

            var bulkResource = new QueueBulkResource { Ids = new List<int> { 1, 2 } };

            Subject.RemoveMany(bulkResource, null);

            Mocker.GetMock<IPendingReleaseService>()
                .Verify(s => s.RemovePendingQueueItems(1), Times.Once());
            Mocker.GetMock<IPendingReleaseService>()
                .Verify(s => s.RemovePendingQueueItems(2), Times.Once());
        }

        [Test]
        public void get_resource_by_id_should_throw_not_implemented()
        {
            Assert.Throws<NotImplementedException>(() => Subject.GetResourceByIdWithErrorHandler(1));
        }
    }
}
