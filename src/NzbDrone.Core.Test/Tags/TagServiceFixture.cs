using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Tags
{
    [TestFixture]
    public class TagServiceFixture : CoreTest<TagService>
    {
        [Test]
        public void should_get_tag_by_id()
        {
            var tag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(tag);

            Subject.GetTag(1).Should().Be(tag);
        }

        [Test]
        public void should_get_tag_by_label_string()
        {
            var tag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.GetByLabel("test"))
                  .Returns(tag);

            Subject.GetTag("test").Label.Should().Be("test");
        }

        [Test]
        public void should_get_tag_by_numeric_string_as_id()
        {
            var tag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(tag);

            Subject.GetTag("1").Should().Be(tag);
        }

        [Test]
        public void should_return_all_tags_ordered_by_label()
        {
            var tags = new List<Tag>
            {
                new Tag { Id = 1, Label = "beta" },
                new Tag { Id = 2, Label = "alpha" }
            };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.All())
                  .Returns(tags);

            var result = Subject.All();

            result.Should().HaveCount(2);
            result.First().Label.Should().Be("alpha");
        }

        [Test]
        public void should_add_tag_and_publish_event()
        {
            var tag = new Tag { Label = "Test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.FindByLabel("Test"))
                  .Returns((Tag)null);

            Subject.Add(tag);

            Mocker.GetMock<ITagRepository>()
                  .Verify(v => v.Insert(It.Is<Tag>(t => t.Label == "test")), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<TagsUpdatedEvent>()), Times.Once());
        }

        [Test]
        public void should_return_existing_tag_if_label_already_exists()
        {
            var existingTag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.FindByLabel("Test"))
                  .Returns(existingTag);

            var result = Subject.Add(new Tag { Label = "Test" });

            result.Should().Be(existingTag);

            Mocker.GetMock<ITagRepository>()
                  .Verify(v => v.Insert(It.IsAny<Tag>()), Times.Never());
        }

        [Test]
        public void should_update_tag_label_to_lowercase()
        {
            var tag = new Tag { Id = 1, Label = "Updated" };

            Subject.Update(tag);

            Mocker.GetMock<ITagRepository>()
                  .Verify(v => v.Update(It.Is<Tag>(t => t.Label == "updated")), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<TagsUpdatedEvent>()), Times.Once());
        }

        [Test]
        public void should_delete_tag_when_not_in_use()
        {
            var tag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(tag);

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<DelayProfile>());

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IImportList>());

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<INotification>());

            Mocker.GetMock<IReleaseProfileService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<ReleaseProfile>());

            Mocker.GetMock<IReleaseProfileService>()
                  .Setup(s => s.AllExcludedForTag(1))
                  .Returns(new List<ReleaseProfile>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<Series>());

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IIndexer>());

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<AutoTag>());

            Mocker.GetMock<IDownloadClientFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IDownloadClient>());

            Subject.Delete(1);

            Mocker.GetMock<ITagRepository>()
                  .Verify(v => v.Delete(1), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<TagsUpdatedEvent>()), Times.Once());
        }

        [Test]
        public void should_throw_if_tag_is_in_use_on_delete()
        {
            var tag = new Tag { Id = 1, Label = "test" };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(tag);

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<DelayProfile> { new DelayProfile { Id = 1 } });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IImportList>());

            Mocker.GetMock<INotificationFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<INotification>());

            Mocker.GetMock<IReleaseProfileService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<ReleaseProfile>());

            Mocker.GetMock<IReleaseProfileService>()
                  .Setup(s => s.AllExcludedForTag(1))
                  .Returns(new List<ReleaseProfile>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<Series>());

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IIndexer>());

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<AutoTag>());

            Mocker.GetMock<IDownloadClientFactory>()
                  .Setup(s => s.AllForTag(1))
                  .Returns(new List<IDownloadClient>());

            Assert.Throws<ModelConflictException>(() => Subject.Delete(1));
        }

        [Test]
        public void should_get_multiple_tags_by_ids()
        {
            var tags = new List<Tag>
            {
                new Tag { Id = 1, Label = "tag1" },
                new Tag { Id = 2, Label = "tag2" }
            };

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns(tags);

            var result = Subject.GetTags(new List<int> { 1, 2 });

            result.Should().HaveCount(2);
        }
    }
}
