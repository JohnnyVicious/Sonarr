using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class CustomFormatCalculationServiceFixture : CoreTest<CustomFormatCalculationService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Test Series")
                .Build();

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat>());
        }

        [Test]
        public void should_return_empty_when_no_custom_formats_exist()
        {
            var remoteEpisode = new RemoteEpisode
            {
                Series = _series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.HDTV720p), EpisodeNumbers = new[] { 1 } },
                Languages = new List<Language> { Language.English },
                Release = new ReleaseInfo { IndexerFlags = 0 }
            };

            var result = Subject.ParseCustomFormat(remoteEpisode, 1000);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_match_custom_format_when_all_specs_match()
        {
            var mockSpec = new Mock<ICustomFormatSpecification>();
            mockSpec.Setup(s => s.IsSatisfiedBy(It.IsAny<CustomFormatInput>())).Returns(true);

            var customFormat = new CustomFormat("TestFormat")
            {
                Id = 1,
                Specifications = new List<ICustomFormatSpecification> { mockSpec.Object }
            };

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat> { customFormat });

            var remoteEpisode = new RemoteEpisode
            {
                Series = _series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.HDTV720p), EpisodeNumbers = new[] { 1 } },
                Languages = new List<Language> { Language.English },
                Release = new ReleaseInfo { IndexerFlags = 0 }
            };

            var result = Subject.ParseCustomFormat(remoteEpisode, 1000);

            result.Should().HaveCount(1);
            result.First().Name.Should().Be("TestFormat");
        }

        [Test]
        public void should_not_match_custom_format_when_spec_does_not_match()
        {
            var mockSpec = new Mock<ICustomFormatSpecification>();
            mockSpec.Setup(s => s.IsSatisfiedBy(It.IsAny<CustomFormatInput>())).Returns(false);

            var customFormat = new CustomFormat("TestFormat")
            {
                Id = 1,
                Specifications = new List<ICustomFormatSpecification> { mockSpec.Object }
            };

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat> { customFormat });

            var remoteEpisode = new RemoteEpisode
            {
                Series = _series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.HDTV720p), EpisodeNumbers = new[] { 1 } },
                Languages = new List<Language> { Language.English },
                Release = new ReleaseInfo { IndexerFlags = 0 }
            };

            var result = Subject.ParseCustomFormat(remoteEpisode, 1000);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_parse_custom_format_for_episode_file_with_scene_name()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.SceneName = "Test.Series.S01E01.720p.HDTV")
                .With(e => e.Quality = new QualityModel(Quality.HDTV720p))
                .With(e => e.Languages = new List<Language> { Language.English })
                .With(e => e.RelativePath = "Test Series/Season 1/episode.mkv")
                .Build();

            var result = Subject.ParseCustomFormat(episodeFile, _series);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_parse_custom_format_for_episode_file_with_original_file_path()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.SceneName = null)
                .With(e => e.OriginalFilePath = "Original/Path/episode.mkv")
                .With(e => e.Quality = new QualityModel(Quality.HDTV720p))
                .With(e => e.Languages = new List<Language> { Language.English })
                .With(e => e.RelativePath = "Test Series/Season 1/episode.mkv")
                .Build();

            var result = Subject.ParseCustomFormat(episodeFile, _series);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_parse_custom_format_for_episode_file_with_relative_path()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.SceneName = null)
                .With(e => e.OriginalFilePath = null)
                .With(e => e.Quality = new QualityModel(Quality.HDTV720p))
                .With(e => e.Languages = new List<Language> { Language.English })
                .With(e => e.RelativePath = "Test Series/Season 1/episode.mkv")
                .Build();

            var result = Subject.ParseCustomFormat(episodeFile, _series);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_parse_custom_format_for_blocklist()
        {
            var blocklist = Builder<Blocklist>.CreateNew()
                .With(b => b.SourceTitle = "Test.Series.S01E01.720p.HDTV")
                .With(b => b.Quality = new QualityModel(Quality.HDTV720p))
                .With(b => b.Languages = new List<Language> { Language.English })
                .With(b => b.IndexerFlags = 0)
                .Build();

            var result = Subject.ParseCustomFormat(blocklist, _series);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_parse_custom_format_for_history()
        {
            var history = Builder<EpisodeHistory>.CreateNew()
                .With(h => h.SourceTitle = "Test.Series.S01E01.720p.HDTV")
                .With(h => h.Quality = new QualityModel(Quality.HDTV720p))
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Data = new Dictionary<string, string>
                {
                    { "size", "1000000" },
                    { "indexerFlags", "0" },
                    { "releaseType", "SingleEpisode" }
                })
                .Build();

            var result = Subject.ParseCustomFormat(history, _series);

            result.Should().BeEmpty();
        }

        [Test]
        public void should_return_results_ordered_by_name()
        {
            var mockSpec = new Mock<ICustomFormatSpecification>();
            mockSpec.Setup(s => s.IsSatisfiedBy(It.IsAny<CustomFormatInput>())).Returns(true);

            var formatB = new CustomFormat("Bravo") { Id = 1, Specifications = new List<ICustomFormatSpecification> { mockSpec.Object } };
            var formatA = new CustomFormat("Alpha") { Id = 2, Specifications = new List<ICustomFormatSpecification> { mockSpec.Object } };
            var formatC = new CustomFormat("Charlie") { Id = 3, Specifications = new List<ICustomFormatSpecification> { mockSpec.Object } };

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat> { formatB, formatA, formatC });

            var remoteEpisode = new RemoteEpisode
            {
                Series = _series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.HDTV720p), EpisodeNumbers = new[] { 1 } },
                Languages = new List<Language> { Language.English },
                Release = new ReleaseInfo { IndexerFlags = 0 }
            };

            var result = Subject.ParseCustomFormat(remoteEpisode, 1000);

            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Alpha");
            result[1].Name.Should().Be("Bravo");
            result[2].Name.Should().Be("Charlie");
        }

        [Test]
        public void should_parse_custom_format_for_local_episode_with_scene_name()
        {
            var localEpisode = new LocalEpisode
            {
                Series = _series,
                SceneName = "Test.Series.S01E01.720p.HDTV",
                Quality = new QualityModel(Quality.HDTV720p),
                Languages = new List<Language> { Language.English },
                Size = 1000000,
                ReleaseGroup = "TestGroup"
            };

            var result = Subject.ParseCustomFormat(localEpisode, "episode.mkv");

            result.Should().BeEmpty();
        }
    }
}
