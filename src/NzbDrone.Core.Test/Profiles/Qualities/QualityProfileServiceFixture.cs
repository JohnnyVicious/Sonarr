using System.Collections.Generic; // NOSONAR
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Profiles.Qualities
{
    [TestFixture]
    public class QualityProfileServiceFixture : CoreTest<QualityProfileService>
    {
        private QualityProfile _profile;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile
            {
                Id = 1,
                Name = "Test",
                Cutoff = Quality.HDTV720p.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem { Quality = Quality.SDTV, Allowed = true },
                    new QualityProfileQualityItem { Quality = Quality.HDTV720p, Allowed = true }
                },
                FormatItems = new List<ProfileFormatItem>()
            };
        }

        [Test]
        public void should_add_profile()
        {
            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.Insert(_profile))
                  .Returns(_profile);

            Subject.Add(_profile);

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Insert(_profile), Times.Once());
        }

        [Test]
        public void should_update_profile_and_publish_event()
        {
            Subject.Update(_profile);

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Update(_profile), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<QualityProfileUpdatedEvent>(e => e.Id == _profile.Id)), Times.Once());
        }

        [Test]
        public void should_delete_profile_when_not_in_use()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series>());

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>());

            Subject.Delete(1);

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Delete(1), Times.Once());
        }

        [Test]
        public void should_throw_when_deleting_profile_in_use_by_series()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series> { new Series { QualityProfileId = 1 } });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>());

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(_profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(1));

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_throw_when_deleting_profile_in_use_by_import_list()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series>());

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition> { new ImportListDefinition { QualityProfileId = 1 } });

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(_profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(1));
        }

        [Test]
        public void should_return_all_profiles()
        {
            var profiles = new List<QualityProfile> { _profile };

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(profiles);

            Subject.All().Should().HaveCount(1);
        }

        [Test]
        public void should_get_profile_by_id()
        {
            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(_profile);

            Subject.Get(1).Name.Should().Be("Test");
        }

        [Test]
        public void should_check_if_profile_exists()
        {
            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.Exists(1))
                  .Returns(true);

            Subject.Exists(1).Should().BeTrue();
        }

        [Test]
        public void should_get_default_profile()
        {
            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat>());

            var profile = Subject.GetDefaultProfile("Test", Quality.HDTV720p, Quality.SDTV, Quality.HDTV720p);

            profile.Name.Should().Be("Test");
            profile.Items.Should().NotBeEmpty();
        }

        [Test]
        public void should_not_create_default_profiles_on_startup_if_profiles_exist()
        {
            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { _profile });

            Subject.Handle(new Lifecycle.ApplicationStartedEvent());

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Insert(It.IsAny<QualityProfile>()), Times.Never());
        }

        [Test]
        public void should_add_custom_format_to_all_profiles_on_custom_format_added()
        {
            var customFormat = new CustomFormat { Id = 1, Name = "TestFormat" };

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { _profile });

            Subject.Handle(new CustomFormats.Events.CustomFormatAddedEvent(customFormat));

            Mocker.GetMock<IQualityProfileRepository>()
                  .Verify(v => v.Update(It.Is<QualityProfile>(p => p.FormatItems.Any(f => f.Format == customFormat))), Times.Once());
        }
    }
}
