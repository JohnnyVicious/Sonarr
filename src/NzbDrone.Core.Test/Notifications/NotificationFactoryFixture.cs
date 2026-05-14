using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Notifications
{
    [TestFixture]
    public class NotificationFactoryFixture : CoreTest<NotificationFactory>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<INotificationStatusService>()
                  .Setup(s => s.GetBlockedProviders())
                  .Returns(new List<NotificationStatus>());
        }

        [Test]
        public void set_provider_characteristics_should_set_on_grab_support()
        {
            var mockNotification = new Mock<INotification>();
            mockNotification.SetupGet(s => s.SupportsOnGrab).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnDownload).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnUpgrade).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnImportComplete).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnRename).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnSeriesAdd).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnSeriesDelete).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDelete).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDeleteForUpgrade).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnHealthIssue).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnHealthRestored).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnApplicationUpdate).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnManualInteractionRequired).Returns(false);

            var definition = new NotificationDefinition();

            Subject.SetProviderCharacteristics(mockNotification.Object, definition);

            definition.SupportsOnGrab.Should().BeTrue();
            definition.SupportsOnDownload.Should().BeFalse();
        }

        [Test]
        public void set_provider_characteristics_should_set_all_supports_flags()
        {
            var mockNotification = new Mock<INotification>();
            mockNotification.SetupGet(s => s.SupportsOnGrab).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnDownload).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnUpgrade).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnImportComplete).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnRename).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnSeriesAdd).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnSeriesDelete).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDelete).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDeleteForUpgrade).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnHealthIssue).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnHealthRestored).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnApplicationUpdate).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnManualInteractionRequired).Returns(true);

            var definition = new NotificationDefinition();

            Subject.SetProviderCharacteristics(mockNotification.Object, definition);

            definition.SupportsOnGrab.Should().BeTrue();
            definition.SupportsOnDownload.Should().BeTrue();
            definition.SupportsOnUpgrade.Should().BeTrue();
            definition.SupportsOnImportComplete.Should().BeTrue();
            definition.SupportsOnRename.Should().BeTrue();
            definition.SupportsOnSeriesAdd.Should().BeTrue();
            definition.SupportsOnSeriesDelete.Should().BeTrue();
            definition.SupportsOnEpisodeFileDelete.Should().BeTrue();
            definition.SupportsOnEpisodeFileDeleteForUpgrade.Should().BeTrue();
            definition.SupportsOnHealthIssue.Should().BeTrue();
            definition.SupportsOnHealthRestored.Should().BeTrue();
            definition.SupportsOnApplicationUpdate.Should().BeTrue();
            definition.SupportsOnManualInteractionRequired.Should().BeTrue();
        }

        [Test]
        public void set_provider_characteristics_should_set_mixed_supports()
        {
            var mockNotification = new Mock<INotification>();
            mockNotification.SetupGet(s => s.SupportsOnGrab).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnDownload).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnUpgrade).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnImportComplete).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnRename).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnSeriesAdd).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnSeriesDelete).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDelete).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnEpisodeFileDeleteForUpgrade).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnHealthIssue).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnHealthRestored).Returns(false);
            mockNotification.SetupGet(s => s.SupportsOnApplicationUpdate).Returns(true);
            mockNotification.SetupGet(s => s.SupportsOnManualInteractionRequired).Returns(false);

            var definition = new NotificationDefinition();

            Subject.SetProviderCharacteristics(mockNotification.Object, definition);

            definition.SupportsOnGrab.Should().BeTrue();
            definition.SupportsOnDownload.Should().BeTrue();
            definition.SupportsOnUpgrade.Should().BeFalse();
            definition.SupportsOnImportComplete.Should().BeFalse();
            definition.SupportsOnRename.Should().BeTrue();
            definition.SupportsOnSeriesAdd.Should().BeFalse();
            definition.SupportsOnSeriesDelete.Should().BeFalse();
            definition.SupportsOnEpisodeFileDelete.Should().BeTrue();
            definition.SupportsOnEpisodeFileDeleteForUpgrade.Should().BeFalse();
            definition.SupportsOnHealthIssue.Should().BeTrue();
            definition.SupportsOnHealthRestored.Should().BeFalse();
            definition.SupportsOnApplicationUpdate.Should().BeTrue();
            definition.SupportsOnManualInteractionRequired.Should().BeFalse();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_grab()
        {
            var definition = new NotificationDefinition { OnGrab = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_download()
        {
            var definition = new NotificationDefinition { OnDownload = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_rename()
        {
            var definition = new NotificationDefinition { OnRename = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_health_issue()
        {
            var definition = new NotificationDefinition { OnHealthIssue = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_false_when_nothing_enabled()
        {
            var definition = new NotificationDefinition();
            definition.Enable.Should().BeFalse();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_series_add()
        {
            var definition = new NotificationDefinition { OnSeriesAdd = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_application_update()
        {
            var definition = new NotificationDefinition { OnApplicationUpdate = true };
            definition.Enable.Should().BeTrue();
        }

        [Test]
        public void notification_definition_enable_should_be_true_when_on_episode_file_delete()
        {
            var definition = new NotificationDefinition { OnEpisodeFileDelete = true };
            definition.Enable.Should().BeTrue();
        }
    }
}
