using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.SeriesStats;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Series;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class SeriesControllerFixture : TestBase<SeriesController>
    {
        private NzbDrone.Core.Tv.Series _series;

        [SetUp]
        public void Setup()
        {
            _series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Title = "Test Series",
                TvdbId = 12345,
                Path = "/tv/Test Series",
                QualityProfileId = 1,
                Monitored = true,
                Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true }
                },
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(1))
                .Returns(_series);

            Mocker.GetMock<ISeriesStatisticsService>()
                .Setup(s => s.SeriesStatistics())
                .Returns(new List<SeriesStatistics>());

            Mocker.GetMock<ISeriesStatisticsService>()
                .Setup(s => s.SeriesStatistics(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new SeriesStatistics());

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>());

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                .Returns("/tv");

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("/api/v5/series/1");

            Subject.Url = urlHelper.Object;
            Subject.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void should_get_all_series()
        {
            var seriesList = new List<NzbDrone.Core.Tv.Series> { _series };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(seriesList);

            var result = Subject.AllSeries(null, Array.Empty<SeriesSubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].Title.Should().Be("Test Series");
            result.Value[0].TvdbId.Should().Be(12345);
        }

        [Test]
        public void should_get_series_by_tvdb_id()
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.FindByTvdbId(12345))
                .Returns(_series);

            var result = Subject.AllSeries(12345, Array.Empty<SeriesSubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].TvdbId.Should().Be(12345);
        }

        [Test]
        public void should_return_empty_when_tvdb_id_not_found()
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.FindByTvdbId(99999))
                .Returns((NzbDrone.Core.Tv.Series)null);

            var result = Subject.AllSeries(99999, Array.Empty<SeriesSubresource>());

            result.Value.Should().BeEmpty();
        }

        [Test]
        public void should_get_series_by_id()
        {
            var result = Subject.GetResourceByIdWithErrorHandler(1, Array.Empty<SeriesSubresource>());

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<SeriesResource>>().Subject;
            okResult.Value.Id.Should().Be(1);
            okResult.Value.Title.Should().Be("Test Series");
        }

        [Test]
        public void should_return_not_found_for_missing_series()
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(999))
                .Throws(new NzbDrone.Core.Datastore.ModelNotFoundException(typeof(NzbDrone.Core.Tv.Series), 999));

            var result = Subject.GetResourceByIdWithErrorHandler(999, Array.Empty<SeriesSubresource>());

            result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
        }

        [Test]
        public void should_delete_series()
        {
            Subject.DeleteSeries(1);

            Mocker.GetMock<ISeriesService>()
                .Verify(s => s.DeleteSeries(It.Is<List<int>>(l => l.Contains(1)), false, false), Times.Once());
        }

        [Test]
        public void should_delete_series_with_files()
        {
            Subject.DeleteSeries(1, deleteFiles: true, addImportListExclusion: false);

            Mocker.GetMock<ISeriesService>()
                .Verify(s => s.DeleteSeries(It.Is<List<int>>(l => l.Contains(1)), true, false), Times.Once());
        }

        [Test]
        public void should_delete_series_with_import_list_exclusion()
        {
            Subject.DeleteSeries(1, deleteFiles: false, addImportListExclusion: true);

            Mocker.GetMock<ISeriesService>()
                .Verify(s => s.DeleteSeries(It.Is<List<int>>(l => l.Contains(1)), false, true), Times.Once());
        }

        [Test]
        public void should_add_series()
        {
            var seriesResource = new SeriesResource
            {
                Title = "New Series",
                TvdbId = 54321,
                Path = "/tv/New Series",
                QualityProfileId = 1,
                Seasons = new List<SeasonResource>(),
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            var addedSeries = new NzbDrone.Core.Tv.Series
            {
                Id = 5,
                Title = "New Series",
                TvdbId = 54321,
                Path = "/tv/New Series",
                QualityProfileId = 1,
                Seasons = new List<Season>(),
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            Mocker.GetMock<IAddSeriesService>()
                .Setup(s => s.AddSeries(It.IsAny<NzbDrone.Core.Tv.Series>()))
                .Returns(addedSeries);

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(5))
                .Returns(addedSeries);

            Subject.AddSeries(seriesResource);

            Mocker.GetMock<IAddSeriesService>()
                .Verify(s => s.AddSeries(It.Is<NzbDrone.Core.Tv.Series>(m => m.TvdbId == 54321)), Times.Once());
        }

        [Test]
        public void should_update_series()
        {
            var seriesResource = new SeriesResource
            {
                Id = 1,
                Title = "Updated Series",
                TvdbId = 12345,
                Path = "/tv/Test Series",
                QualityProfileId = 1,
                Seasons = new List<SeasonResource>(),
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.UpdateSeries(It.IsAny<NzbDrone.Core.Tv.Series>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(_series);

            Subject.UpdateSeries(seriesResource, moveFiles: false);

            Mocker.GetMock<ISeriesService>()
                .Verify(s => s.UpdateSeries(It.IsAny<NzbDrone.Core.Tv.Series>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_push_move_command_when_move_files_true()
        {
            var seriesResource = new SeriesResource
            {
                Id = 1,
                Title = "Test Series",
                TvdbId = 12345,
                Path = "/tv/New Path",
                QualityProfileId = 1,
                Seasons = new List<SeasonResource>(),
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.UpdateSeries(It.IsAny<NzbDrone.Core.Tv.Series>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(_series);

            Subject.UpdateSeries(seriesResource, moveFiles: true);

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.Is<MoveSeriesCommand>(c =>
                    c.SeriesId == 1 &&
                    c.SourcePath == "/tv/Test Series" &&
                    c.DestinationPath == "/tv/New Path"),
                    It.IsAny<CommandPriority>(),
                    It.IsAny<CommandTrigger>()), Times.Once());
        }

        [Test]
        public void should_not_push_move_command_when_move_files_false()
        {
            var seriesResource = new SeriesResource
            {
                Id = 1,
                Title = "Test Series",
                TvdbId = 12345,
                Path = "/tv/New Path",
                QualityProfileId = 1,
                Seasons = new List<SeasonResource>(),
                Images = new List<MediaCover>(),
                Tags = new HashSet<int>()
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.UpdateSeries(It.IsAny<NzbDrone.Core.Tv.Series>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(_series);

            Subject.UpdateSeries(seriesResource, moveFiles: false);

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<MoveSeriesCommand>(),
                    It.IsAny<CommandPriority>(),
                    It.IsAny<CommandTrigger>()), Times.Never());
        }
    }
}
