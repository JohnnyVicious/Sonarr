using System; // NOSONAR
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Update.History.Events;

namespace NzbDrone.Core.Test.Notifications
{
    [TestFixture]
    public class NotificationServiceFixture : CoreTest<NotificationService>
    {
        private Mock<INotification> _notification;
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _notification = new Mock<INotification>();
            _notification.SetupGet(s => s.Definition)
                         .Returns(new NotificationDefinition
                         {
                             Id = 1,
                             Name = "TestNotification",
                             OnGrab = true,
                             OnDownload = true,
                             OnUpgrade = true,
                             OnRename = true,
                             OnHealthIssue = true,
                             IncludeHealthWarnings = true,
                             OnHealthRestored = true,
                             OnApplicationUpdate = true,
                             OnSeriesAdd = true,
                             OnSeriesDelete = true,
                             OnImportComplete = true,
                             Tags = new HashSet<int>()
                         });

            _series = new Series
            {
                Id = 1,
                Title = "Test Series",
                Tags = new HashSet<int>()
            };
        }

        private EpisodeGrabbedEvent GetGrabbedEvent()
        {
            return new EpisodeGrabbedEvent(new RemoteEpisode
            {
                Series = _series,
                Episodes = new List<Episode> { new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    Quality = new QualityModel(Quality.HDTV720p)
                }
            })
            {
                DownloadClient = "Sabnzbd",
                DownloadClientName = "Sab",
                DownloadId = "123"
            };
        }

        [Test]
        public void should_trigger_on_grab_for_enabled_notifications()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnGrabEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(GetGrabbedEvent());

            _notification.Verify(v => v.OnGrab(It.IsAny<GrabMessage>()), Times.Once());
        }

        [Test]
        public void should_not_trigger_on_grab_when_no_notifications_enabled()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnGrabEnabled(true))
                  .Returns(new List<INotification>());

            Subject.Handle(GetGrabbedEvent());

            _notification.Verify(v => v.OnGrab(It.IsAny<GrabMessage>()), Times.Never());
        }

        [Test]
        public void should_record_failure_on_grab_exception()
        {
            _notification.Setup(s => s.OnGrab(It.IsAny<GrabMessage>()))
                         .Throws(new Exception("Test exception"));

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnGrabEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(GetGrabbedEvent());

            Mocker.GetMock<INotificationStatusService>()
                  .Verify(v => v.RecordFailure(1), Times.Once());
        }

        [Test]
        public void should_record_success_on_grab()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnGrabEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(GetGrabbedEvent());

            Mocker.GetMock<INotificationStatusService>()
                  .Verify(v => v.RecordSuccess(1), Times.Once());
        }

        [Test]
        public void should_skip_notification_when_tags_do_not_intersect()
        {
            _notification.SetupGet(s => s.Definition)
                         .Returns(new NotificationDefinition
                         {
                             Id = 1,
                             Name = "TestNotification",
                             OnGrab = true,
                             Tags = new HashSet<int> { 10 }
                         });

            _series.Tags = new HashSet<int> { 20 };

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnGrabEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(GetGrabbedEvent());

            _notification.Verify(v => v.OnGrab(It.IsAny<GrabMessage>()), Times.Never());
        }

        [Test]
        public void should_trigger_on_rename()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnRenameEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new SeriesRenamedEvent(_series, new List<RenamedEpisodeFile>()));

            _notification.Verify(v => v.OnRename(_series, It.IsAny<List<RenamedEpisodeFile>>()), Times.Once());
        }

        [Test]
        public void should_trigger_on_health_issue_for_error()
        {
            var healthCheck = new HealthCheck.HealthCheck(typeof(NotificationServiceFixture), HealthCheckResult.Error, HealthCheckReason.ServerNotification, "Test error");

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnHealthIssueEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new HealthCheckFailedEvent(healthCheck, false));

            _notification.Verify(v => v.OnHealthIssue(healthCheck), Times.Once());
        }

        [Test]
        public void should_not_trigger_on_health_issue_during_startup_grace_period()
        {
            var healthCheck = new HealthCheck.HealthCheck(typeof(NotificationServiceFixture), HealthCheckResult.Error, HealthCheckReason.ServerNotification, "Test error");

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnHealthIssueEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new HealthCheckFailedEvent(healthCheck, true));

            _notification.Verify(v => v.OnHealthIssue(It.IsAny<HealthCheck.HealthCheck>()), Times.Never());
        }

        [Test]
        public void should_trigger_on_application_update()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnApplicationUpdateEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new UpdateInstalledEvent(new Version(3, 0, 0), new Version(3, 0, 1)));

            _notification.Verify(v => v.OnApplicationUpdate(It.IsAny<ApplicationUpdateMessage>()), Times.Once());
        }

        [Test]
        public void should_trigger_on_series_add()
        {
            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnSeriesAddEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new Tv.Events.SeriesAddCompletedEvent(_series));

            _notification.Verify(v => v.OnSeriesAdd(It.IsAny<SeriesAddMessage>()), Times.Once());
        }

        [Test]
        public void should_not_trigger_on_health_warning_when_not_including_warnings()
        {
            _notification.SetupGet(s => s.Definition)
                         .Returns(new NotificationDefinition
                         {
                             Id = 1,
                             Name = "TestNotification",
                             OnHealthIssue = true,
                             IncludeHealthWarnings = false,
                             Tags = new HashSet<int>()
                         });

            var healthCheck = new HealthCheck.HealthCheck(typeof(NotificationServiceFixture), HealthCheckResult.Warning, HealthCheckReason.ServerNotification, "Test warning");

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.OnHealthIssueEnabled(true))
                  .Returns(new List<INotification> { _notification.Object });

            Subject.Handle(new HealthCheckFailedEvent(healthCheck, false));

            _notification.Verify(v => v.OnHealthIssue(It.IsAny<HealthCheck.HealthCheck>()), Times.Never());
        }
    }
}
