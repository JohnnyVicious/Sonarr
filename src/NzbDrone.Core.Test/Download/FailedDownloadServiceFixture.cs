using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class FailedDownloadServiceFixture : CoreTest<FailedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                .With(h => h.Status = DownloadItemStatus.Failed)
                .With(h => h.DownloadId = "123")
                .With(h => h.Title = "Drone.S01E01.HDTV")
                .With(h => h.IsEncrypted = false)
                .Build();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                .With(c => c.State = TrackedDownloadState.Downloading)
                .With(c => c.DownloadItem = completed)
                .Build();
        }

        [Test]
        public void should_not_check_when_state_is_not_downloading_or_import_blocked()
        {
            _trackedDownload.State = TrackedDownloadState.Imported;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
        }

        [Test]
        public void should_set_state_to_failed_pending_when_download_failed_and_has_history()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory { DownloadId = "123", EventType = EpisodeHistoryEventType.Grabbed }
                  });

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_warn_when_download_failed_and_no_grabbed_history()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>());

            Subject.Check(_trackedDownload);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void should_set_state_to_failed_pending_when_encrypted()
        {
            _trackedDownload.DownloadItem.IsEncrypted = true;
            _trackedDownload.DownloadItem.Status = DownloadItemStatus.Completed;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory { DownloadId = "123", EventType = EpisodeHistoryEventType.Grabbed }
                  });

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_not_process_failed_when_state_is_not_failed_pending()
        {
            _trackedDownload.State = TrackedDownloadState.Downloading;

            Subject.ProcessFailed(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
        }

        [Test]
        public void should_process_failed_and_set_state_to_failed()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory
                      {
                          DownloadId = "123",
                          EventType = EpisodeHistoryEventType.Grabbed,
                          EpisodeId = 1,
                          SeriesId = 1,
                          SourceTitle = "Drone.S01E01.HDTV"
                      }
                  });

            Subject.ProcessFailed(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Failed);
        }

        [Test]
        public void should_publish_download_failed_event_on_process_failed()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory
                      {
                          DownloadId = "123",
                          EventType = EpisodeHistoryEventType.Grabbed,
                          EpisodeId = 1,
                          SeriesId = 1,
                          SourceTitle = "Drone.S01E01.HDTV"
                      }
                  });

            Subject.ProcessFailed(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadFailedEvent>()), Times.Once());
        }

        [Test]
        public void should_not_process_failed_when_no_grabbed_history()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>());

            Subject.ProcessFailed(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void should_use_message_from_download_item_when_failed()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;
            _trackedDownload.DownloadItem.Message = "Disk full";

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory
                      {
                          DownloadId = "123",
                          EventType = EpisodeHistoryEventType.Grabbed,
                          EpisodeId = 1,
                          SeriesId = 1,
                          SourceTitle = "Drone.S01E01.HDTV"
                      }
                  });

            Subject.ProcessFailed(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DownloadFailedEvent>(e => e.Message == "Disk full")), Times.Once());
        }

        [Test]
        public void should_check_from_import_blocked_state()
        {
            _trackedDownload.State = TrackedDownloadState.ImportBlocked;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("123", EpisodeHistoryEventType.Grabbed))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory { DownloadId = "123", EventType = EpisodeHistoryEventType.Grabbed }
                  });

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }
    }
}
