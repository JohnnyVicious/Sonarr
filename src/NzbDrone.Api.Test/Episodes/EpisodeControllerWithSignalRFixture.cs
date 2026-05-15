using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using V3EpisodeControllerWithSignalR = Sonarr.Api.V3.Episodes.EpisodeControllerWithSignalR;
using V3EpisodeResource = Sonarr.Api.V3.Episodes.EpisodeResource;
using V5EpisodeControllerWithSignalR = Sonarr.Api.V5.Episodes.EpisodeControllerWithSignalR;
using V5EpisodeResource = Sonarr.Api.V5.Episodes.EpisodeResource;

namespace NzbDrone.Api.Test.Episodes
{
    [TestFixture]
    public class EpisodeControllerWithSignalRFixture : TestBase
    {
        private NzbDrone.Core.Tv.Series _series;

        [SetUp]
        public void SetUp()
        {
            _series = new NzbDrone.Core.Tv.Series
            {
                Id = 1,
                Path = "/series",
                QualityProfile = new LazyLoaded<QualityProfile>(new QualityProfile())
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(_series.Id))
                .Returns(_series);

            Mocker.GetMock<IUpgradableSpecification>()
                .Setup(s => s.QualityCutoffNotMet(It.IsAny<QualityProfile>(), It.IsAny<QualityModel>(), It.IsAny<QualityModel>()))
                .Returns(false);

            Mocker.GetMock<ICustomFormatCalculationService>()
                .Setup(s => s.ParseCustomFormat(It.IsAny<EpisodeFile>(), It.IsAny<NzbDrone.Core.Tv.Series>()))
                .Returns(new List<CustomFormat>());
        }

        [Test]
        public void v3_should_not_map_missing_episode_file()
        {
            var resource = Mocker.Resolve<TestV3EpisodeController>()
                .Map(CreateEpisode(null), includeEpisodeFile: true);

            resource.EpisodeFile.Should().BeNull();
        }

        [Test]
        public void v5_should_not_map_missing_episode_file()
        {
            var resource = Mocker.Resolve<TestV5EpisodeController>()
                .Map(CreateEpisode(null), includeEpisodeFile: true);

            resource.EpisodeFile.Should().BeNull();
        }

        [Test]
        public void v3_should_map_existing_episode_file()
        {
            var resource = Mocker.Resolve<TestV3EpisodeController>()
                .Map(CreateEpisode(CreateEpisodeFile()), includeEpisodeFile: true);

            resource.EpisodeFile.Should().NotBeNull();
            resource.EpisodeFile.Id.Should().Be(2);
        }

        [Test]
        public void v5_should_map_existing_episode_file()
        {
            var resource = Mocker.Resolve<TestV5EpisodeController>()
                .Map(CreateEpisode(CreateEpisodeFile()), includeEpisodeFile: true);

            resource.EpisodeFile.Should().NotBeNull();
            resource.EpisodeFile.Id.Should().Be(2);
        }

        private Episode CreateEpisode(EpisodeFile episodeFile)
        {
            return new Episode
            {
                Id = 3,
                SeriesId = _series.Id,
                Series = _series,
                EpisodeFileId = 2,
                EpisodeFile = new LazyLoaded<EpisodeFile>(episodeFile)
            };
        }

        private EpisodeFile CreateEpisodeFile()
        {
            return new EpisodeFile
            {
                Id = 2,
                SeriesId = _series.Id,
                RelativePath = "Season 01/Episode.mkv",
                Quality = new QualityModel(Quality.HDTV720p),
                Languages = []
            };
        }

        private class TestV3EpisodeController : V3EpisodeControllerWithSignalR
        {
            public TestV3EpisodeController(IEpisodeService episodeService,
                                           ISeriesService seriesService,
                                           IUpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatCalculator,
                                           IBroadcastSignalRMessage signalRBroadcaster)
                : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalRBroadcaster)
            {
            }

            public V3EpisodeResource Map(Episode episode, bool includeEpisodeFile)
            {
                return MapToResource(episode, false, includeEpisodeFile, false);
            }
        }

        private class TestV5EpisodeController : V5EpisodeControllerWithSignalR
        {
            public TestV5EpisodeController(IEpisodeService episodeService,
                                           ISeriesService seriesService,
                                           IUpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatCalculator,
                                           IBroadcastSignalRMessage signalRBroadcaster)
                : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalRBroadcaster)
            {
            }

            public V5EpisodeResource Map(Episode episode, bool includeEpisodeFile)
            {
                return MapToResource(episode, false, includeEpisodeFile, false);
            }
        }
    }
}
