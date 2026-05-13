using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.History;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class HistoryControllerFixture : TestBase<HistoryController>
    {
        private NzbDrone.Core.Tv.Series _series;
        private Episode _episode;
        private QualityProfile _qualityProfile;

        [SetUp]
        public void Setup()
        {
            _qualityProfile = new QualityProfile
            {
                Id = 1,
                Name = "Default",
                Cutoff = Quality.HDTV720p.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem { Quality = Quality.SDTV, Allowed = true },
                    new QualityProfileQualityItem { Quality = Quality.HDTV720p, Allowed = true }
                },
                FormatItems = new List<ProfileFormatItem>()
            };

            _series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Title = "Test Series",
                TvdbId = 12345,
                QualityProfileId = 1,
                QualityProfile = new LazyLoaded<QualityProfile>(_qualityProfile),
                Seasons = new List<Season>(),
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                Tags = new HashSet<int>()
            };

            _episode = new Episode
            {
                Id = 10,
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Pilot"
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(1))
                .Returns(_series);

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisode(10))
                .Returns(_episode);

            Mocker.GetMock<ICustomFormatCalculationService>()
                .Setup(s => s.ParseCustomFormat(It.IsAny<EpisodeHistory>(), It.IsAny<NzbDrone.Core.Tv.Series>()))
                .Returns(new List<CustomFormat>());
        }

        [Test]
        public void should_get_history_since_date()
        {
            var date = DateTime.UtcNow.AddDays(-7);
            var historyRecords = new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Id = 1,
                    SeriesId = 1,
                    EpisodeId = 10,
                    SourceTitle = "Test.S01E01.720p",
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.English },
                    Date = DateTime.UtcNow,
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Data = new Dictionary<string, string>(),
                    Series = _series,
                    Episode = _episode
                }
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.Since(date, null))
                .Returns(historyRecords);

            var result = Subject.GetHistorySince(date, null, Array.Empty<HistorySubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].SourceTitle.Should().Be("Test.S01E01.720p");
            result.Value[0].EventType.Should().Be(EpisodeHistoryEventType.Grabbed);
        }

        [Test]
        public void should_get_series_history()
        {
            var historyRecords = new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Id = 1,
                    SeriesId = 1,
                    EpisodeId = 10,
                    SourceTitle = "Test.S01E01.720p",
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.English },
                    Date = DateTime.UtcNow,
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Data = new Dictionary<string, string>()
                }
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.GetBySeries(1, null))
                .Returns(historyRecords);

            var result = Subject.GetSeriesHistory(1, null, Array.Empty<HistorySubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].SeriesId.Should().Be(1);
        }

        [Test]
        public void should_get_season_history()
        {
            var historyRecords = new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Id = 1,
                    SeriesId = 1,
                    EpisodeId = 10,
                    SourceTitle = "Test.S01E01.720p",
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.English },
                    Date = DateTime.UtcNow,
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Data = new Dictionary<string, string>()
                }
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.GetBySeason(1, 1, null))
                .Returns(historyRecords);

            var result = Subject.GetSeasonHistory(1, 1, null, Array.Empty<HistorySubresource>());

            result.Value.Should().HaveCount(1);
        }

        [Test]
        public void should_get_episode_history()
        {
            var historyRecords = new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Id = 1,
                    SeriesId = 1,
                    EpisodeId = 10,
                    SourceTitle = "Test.S01E01.720p",
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.English },
                    Date = DateTime.UtcNow,
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Data = new Dictionary<string, string>()
                }
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.GetByEpisode(10, null))
                .Returns(historyRecords);

            var result = Subject.GetEpisodeHistory(10, null, Array.Empty<HistorySubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].EpisodeId.Should().Be(10);
        }

        [Test]
        public void should_mark_as_failed()
        {
            Subject.MarkAsFailed(1);

            Mocker.GetMock<IFailedDownloadService>()
                .Verify(s => s.MarkAsFailed(1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_get_series_history_with_event_type_filter()
        {
            var historyRecords = new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Id = 1,
                    SeriesId = 1,
                    EpisodeId = 10,
                    SourceTitle = "Test.S01E01.720p",
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.English },
                    Date = DateTime.UtcNow,
                    EventType = EpisodeHistoryEventType.DownloadFolderImported,
                    Data = new Dictionary<string, string>()
                }
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.GetBySeries(1, EpisodeHistoryEventType.DownloadFolderImported))
                .Returns(historyRecords);

            var result = Subject.GetSeriesHistory(1, EpisodeHistoryEventType.DownloadFolderImported, Array.Empty<HistorySubresource>());

            result.Value.Should().HaveCount(1);
            result.Value[0].EventType.Should().Be(EpisodeHistoryEventType.DownloadFolderImported);
        }

        [Test]
        public void should_return_empty_list_for_series_with_no_history()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.GetBySeries(1, null))
                .Returns(new List<EpisodeHistory>());

            var result = Subject.GetSeriesHistory(1, null, Array.Empty<HistorySubresource>());

            result.Value.Should().BeEmpty();
        }
    }
}
