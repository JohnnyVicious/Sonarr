using System; // NOSONAR
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class AddSeriesServiceFixture : CoreTest<AddSeriesService>
    {
        private Series _fakeSeries;

        [SetUp]
        public void Setup()
        {
            _fakeSeries = Builder<Series>.CreateNew()
                .With(s => s.Path = null)
                .With(s => s.TvdbId = 12345)
                .With(s => s.Title = "Test Series")
                .With(s => s.TitleSlug = "test-series")
                .With(s => s.Seasons = new List<Season>())
                .With(s => s.Tags = new HashSet<int>())
                .Build();

            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(It.IsAny<int>()))
                  .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), null))
                  .Returns<Series, NamingConfig>((c, n) => c.Title);

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult());
        }

        [Test]
        public void should_add_series_with_path()
        {
            var newSeries = new Series
            {
                TvdbId = 12345,
                Path = @"C:\Test\TV\Series".AsOsAgnostic()
            };

            Subject.AddSeries(newSeries);

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.AddSeries(It.IsAny<Series>()), Times.Once());
        }

        [Test]
        public void should_set_path_from_root_folder_when_path_is_not_provided()
        {
            var newSeries = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic()
            };

            var result = Subject.AddSeries(newSeries);

            result.Path.Should().Be(Path.Combine(@"C:\Test\TV".AsOsAgnostic(), _fakeSeries.Title));
        }

        [Test]
        public void should_throw_when_series_not_found_on_tvdb()
        {
            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(99999))
                  .Throws(new SeriesNotFoundException(99999));

            var newSeries = new Series
            {
                TvdbId = 99999,
                Path = @"C:\Test\TV\Series".AsOsAgnostic()
            };

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_throw_when_validation_fails()
        {
            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                  {
                      new ValidationFailure("Path", "Invalid path")
                  }));

            var newSeries = new Series
            {
                TvdbId = 12345,
                Path = @"C:\Test\TV\Series".AsOsAgnostic()
            };

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));
        }

        [Test]
        public void should_set_monitored_to_false_when_add_options_monitor_is_none()
        {
            var newSeries = new Series
            {
                TvdbId = 12345,
                Path = @"C:\Test\TV\Series".AsOsAgnostic(),
                AddOptions = new AddSeriesOptions { Monitor = MonitorTypes.None }
            };

            var result = Subject.AddSeries(newSeries);

            result.Monitored.Should().BeFalse();
        }

        [Test]
        public void should_add_multiple_series_skipping_duplicates()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(s => s);

            var series1 = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "series-one"
            };

            var series2 = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "series-one-dupe"
            };

            var result = Subject.AddSeries(new List<Series> { series1, series2 });

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_skip_already_existing_series_on_bulk_add()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int> { 12345 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(s => s);

            var newSeries = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "test-series"
            };

            var result = Subject.AddSeries(new List<Series> { newSeries });

            result.Should().BeEmpty();
        }

        [Test]
        public void should_skip_duplicate_slugs_in_bulk_add()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(s => s);

            var fakeSeries2 = Builder<Series>.CreateNew()
                .With(s => s.Path = null)
                .With(s => s.TvdbId = 67890)
                .With(s => s.Title = "Test Series 2")
                .With(s => s.TitleSlug = "test-series")
                .With(s => s.Seasons = new List<Season>())
                .With(s => s.Tags = new HashSet<int>())
                .Build();

            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(67890))
                  .Returns(new Tuple<Series, List<Episode>>(fakeSeries2, new List<Episode>()));

            var series1 = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "test-series"
            };

            var series2 = new Series
            {
                TvdbId = 67890,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "test-series"
            };

            var result = Subject.AddSeries(new List<Series> { series1, series2 });

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_ignore_errors_when_flag_is_set()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                  {
                      new ValidationFailure("Path", "Invalid")
                  }));

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(s => s);

            var newSeries = new Series
            {
                TvdbId = 12345,
                RootFolderPath = @"C:\Test\TV".AsOsAgnostic(),
                TitleSlug = "test"
            };

            var result = Subject.AddSeries(new List<Series> { newSeries }, ignoreErrors: true);

            result.Should().BeEmpty();
        }
    }
}
