using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesServiceFixture : CoreTest<SeriesService>
    {
        private Series _fakeSeries;

        [SetUp]
        public void Setup()
        {
            _fakeSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.Title = "Test Series")
                .With(s => s.TvdbId = 12345)
                .With(s => s.Tags = new HashSet<int>())
                .With(s => s.Seasons = new List<Season>())
                .Build();

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(It.IsAny<Series>()))
                  .Returns(new AutoTaggingChanges());
        }

        [Test]
        public void should_get_series_by_id()
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(_fakeSeries);

            Subject.GetSeries(1).Should().Be(_fakeSeries);
        }

        [Test]
        public void should_get_series_by_multiple_ids()
        {
            var seriesList = new List<Series> { _fakeSeries };
            var ids = new List<int> { 1 };

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(ids))
                  .Returns(seriesList);

            Subject.GetSeries(ids).Should().HaveCount(1);
        }

        [Test]
        public void should_add_series_and_publish_event()
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(_fakeSeries.Id))
                  .Returns(_fakeSeries);

            Subject.AddSeries(_fakeSeries);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(v => v.Insert(_fakeSeries), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<SeriesAddedEvent>()), Times.Once());
        }

        [Test]
        public void should_add_multiple_series_and_publish_imported_event()
        {
            var seriesList = new List<Series> { _fakeSeries };

            Subject.AddSeries(seriesList);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(v => v.InsertMany(seriesList), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<SeriesImportedEvent>()), Times.Once());
        }

        [Test]
        public void should_delete_series_and_publish_event()
        {
            var seriesIds = new List<int> { 1 };
            var seriesList = new List<Series> { _fakeSeries };

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(seriesIds))
                  .Returns(seriesList);

            Subject.DeleteSeries(seriesIds, true, false);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(v => v.DeleteMany(seriesIds), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<SeriesDeletedEvent>()), Times.Once());
        }

        [Test]
        public void should_get_all_series()
        {
            var seriesList = new List<Series> { _fakeSeries };

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.All())
                  .Returns(seriesList);

            Subject.GetAllSeries().Should().HaveCount(1);
        }

        [Test]
        public void should_find_by_tvdb_id()
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindByTvdbId(12345))
                  .Returns(_fakeSeries);

            Subject.FindByTvdbId(12345).Should().Be(_fakeSeries);
        }

        [Test]
        public void should_find_by_imdb_id()
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindByImdbId("tt1234567"))
                  .Returns(_fakeSeries);

            Subject.FindByImdbId("tt1234567").Should().Be(_fakeSeries);
        }

        [Test]
        public void should_check_series_path_exists()
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.SeriesPathExists("/tv/test"))
                  .Returns(true);

            Subject.SeriesPathExists("/tv/test").Should().BeTrue();
        }

        [Test]
        public void should_update_tags_when_auto_tagging_adds_tags()
        {
            _fakeSeries.Tags = new HashSet<int>();

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(_fakeSeries))
                  .Returns(new AutoTaggingChanges
                  {
                      TagsToAdd = new HashSet<int> { 5 },
                      TagsToRemove = new HashSet<int>()
                  });

            Subject.UpdateTags(_fakeSeries).Should().BeTrue();
            _fakeSeries.Tags.Should().Contain(5);
        }

        [Test]
        public void should_not_update_tags_when_no_changes()
        {
            _fakeSeries.Tags = new HashSet<int>();

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(_fakeSeries))
                  .Returns(new AutoTaggingChanges());

            Subject.UpdateTags(_fakeSeries).Should().BeFalse();
        }

        [Test]
        public void should_remove_tags_from_series()
        {
            _fakeSeries.Tags = new HashSet<int> { 3, 5 };

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(_fakeSeries))
                  .Returns(new AutoTaggingChanges
                  {
                      TagsToAdd = new HashSet<int>(),
                      TagsToRemove = new HashSet<int> { 3 }
                  });

            Subject.UpdateTags(_fakeSeries).Should().BeTrue();
            _fakeSeries.Tags.Should().NotContain(3);
            _fakeSeries.Tags.Should().Contain(5);
        }

        [Test]
        public void should_get_all_series_for_tag()
        {
            var series1 = Builder<Series>.CreateNew()
                .With(s => s.Tags = new HashSet<int> { 1 })
                .Build();

            var series2 = Builder<Series>.CreateNew()
                .With(s => s.Tags = new HashSet<int> { 2 })
                .Build();

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<Series> { series1, series2 });

            Subject.AllForTag(1).Should().HaveCount(1);
            Subject.AllForTag(1).First().Should().Be(series1);
        }

        [Test]
        public void should_remove_add_options()
        {
            Subject.RemoveAddOptions(_fakeSeries);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(v => v.SetFields(_fakeSeries, It.IsAny<System.Linq.Expressions.Expression<System.Func<Series, AddSeriesOptions>>>()), Times.Once());
        }
    }
}
