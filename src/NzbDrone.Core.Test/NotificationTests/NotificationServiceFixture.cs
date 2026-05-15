using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class NotificationServiceFixture : CoreTest<NotificationService>
    {
        [Test]
        public void should_send_manual_interaction_to_untagged_notification_without_series()
        {
            var notification = GivenManualInteractionNotification(1, "untagged");

            GivenManualInteractionNotifications(notification);

            Subject.Handle(CreateManualInteractionEvent(null));

            notification.Verify(
                n => n.OnManualInteractionRequired(It.Is<ManualInteractionRequiredMessage>(m =>
                    m.Message == "Blocked Release" &&
                    m.Series == null &&
                    m.Episode == null)),
                Times.Once());

            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(1), Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordFailure(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_skip_tagged_manual_interaction_notification_without_series()
        {
            var notification = GivenManualInteractionNotification(1, "tagged", 10);

            GivenManualInteractionNotifications(notification);

            Subject.Handle(CreateManualInteractionEvent(null));

            notification.Verify(n => n.OnManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>()), Times.Never());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(It.IsAny<int>()), Times.Never());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordFailure(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_preserve_tag_matching_when_manual_interaction_has_series()
        {
            var notification = GivenManualInteractionNotification(1, "tagged", 10);
            var series = new Series
            {
                Title = "Series",
                Tags = new HashSet<int> { 10 }
            };

            GivenManualInteractionNotifications(notification);

            Subject.Handle(CreateManualInteractionEvent(CreateRemoteEpisode(series)));

            notification.Verify(
                n => n.OnManualInteractionRequired(It.Is<ManualInteractionRequiredMessage>(m =>
                    m.Series == series &&
                    m.Episode.Series == series)),
                Times.Once());

            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(1), Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordFailure(It.IsAny<int>()), Times.Never());
        }

        private void GivenManualInteractionNotifications(params Mock<INotification>[] notifications)
        {
            var providers = new List<INotification>();

            foreach (var notification in notifications)
            {
                providers.Add(notification.Object);
            }

            Mocker.GetMock<INotificationFactory>()
                .Setup(f => f.OnManualInteractionEnabled(It.IsAny<bool>()))
                .Returns(providers);
        }

        private Mock<INotification> GivenManualInteractionNotification(int id, string name, params int[] tags)
        {
            var definition = new NotificationDefinition
            {
                Id = id,
                Name = name,
                Tags = new HashSet<int>(tags)
            };

            var notification = new Mock<INotification>();
            notification.SetupGet(n => n.Definition).Returns(definition);

            return notification;
        }

        private ManualInteractionRequiredEvent CreateManualInteractionEvent(RemoteEpisode remoteEpisode)
        {
            return new ManualInteractionRequiredEvent(
                new TrackedDownload
                {
                    RemoteEpisode = remoteEpisode,
                    DownloadItem = new DownloadClientItem
                    {
                        Title = "Blocked Release",
                        DownloadId = "download-id"
                    }
                },
                null);
        }

        private RemoteEpisode CreateRemoteEpisode(Series series)
        {
            return new RemoteEpisode
            {
                Series = series,
                Episodes = new List<Episode>
                {
                    new Episode
                    {
                        SeasonNumber = 1,
                        EpisodeNumber = 1,
                        Title = "Pilot"
                    }
                },
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    Quality = new QualityModel(Quality.HDTV720p)
                }
            };
        }
    }
}
