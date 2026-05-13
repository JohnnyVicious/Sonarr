using System.Collections.Generic; // NOSONAR
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class EpisodeFileMovingServiceFixture : CoreTest<EpisodeFileMovingService>
    {
        private Series _series;
        private EpisodeFile _episodeFile;
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.Path = @"C:\Test\TV\Series".AsOsAgnostic())
                .With(s => s.SeasonFolder = true)
                .Build();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Id = 1)
                .With(e => e.RelativePath = @"Season 1\episode.mkv")
                .With(e => e.Path = Path.Combine(_series.Path, @"Season 1\episode.mkv".AsOsAgnostic()))
                .Build();

            _localEpisode = new LocalEpisode
            {
                Series = _series,
                Episodes = new List<Episode>
                {
                    Builder<Episode>.CreateNew()
                        .With(e => e.SeasonNumber = 1)
                        .With(e => e.EpisodeNumber = 1)
                        .Build()
                },
                Path = Path.Combine(_series.Path, "Season 1", "episode.mkv".AsOsAgnostic())
            };

            var destinationPath = Path.Combine(_series.Path, "Season 1", "Series - S01E01 - Episode Title.mkv".AsOsAgnostic());

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.BuildFilePath(It.IsAny<List<Episode>>(), It.IsAny<Series>(), It.IsAny<EpisodeFile>(), It.IsAny<string>(), It.IsAny<NamingConfig>(), It.IsAny<List<NzbDrone.Core.CustomFormats.CustomFormat>>()))
                  .Returns(destinationPath);

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.BuildSeasonPath(It.IsAny<Series>(), It.IsAny<int>()))
                  .Returns(Path.Combine(_series.Path, "Season 1".AsOsAgnostic()));

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                  .Returns(@"C:\Test\TV".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IImportScript>()
                  .Setup(s => s.TryImport(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LocalEpisode>(), It.IsAny<EpisodeFile>(), It.IsAny<TransferMode>()))
                  .Returns(ScriptImportDecision.DeferMove);
        }

        [Test]
        public void should_move_episode_file_for_local_episode()
        {
            Subject.MoveEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move), Times.Once());
        }

        [Test]
        public void should_copy_episode_file_using_hardlinks_when_enabled()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CopyUsingHardlinks)
                  .Returns(true);

            Subject.CopyEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.HardLinkOrCopy), Times.Once());
        }

        [Test]
        public void should_copy_episode_file_normally_when_hardlinks_disabled()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CopyUsingHardlinks)
                  .Returns(false);

            Subject.CopyEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Copy), Times.Once());
        }

        [Test]
        public void should_throw_if_source_file_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Assert.Throws<FileNotFoundException>(() => Subject.MoveEpisodeFile(_episodeFile, _localEpisode));
        }

        [Test]
        public void should_throw_if_root_folder_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(false);

            Assert.Throws<RootFolderNotFoundException>(() => Subject.MoveEpisodeFile(_episodeFile, _localEpisode));
        }

        [Test]
        public void should_throw_if_root_folder_path_is_empty()
        {
            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                  .Returns(string.Empty);

            Assert.Throws<RootFolderNotFoundException>(() => Subject.MoveEpisodeFile(_episodeFile, _localEpisode));
        }

        [Test]
        public void should_create_series_folder_if_it_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Subject.MoveEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Once());
        }

        [Test]
        public void should_publish_event_when_folders_are_created()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Subject.MoveEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<EpisodeFolderCreatedEvent>()), Times.Once());
        }

        [Test]
        public void should_move_episode_file_by_series()
        {
            var episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesByFileId(_episodeFile.Id))
                  .Returns(episodes);

            Subject.MoveEpisodeFile(_episodeFile, _series);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move), Times.Once());
        }

        [Test]
        public void should_update_file_date_after_transfer()
        {
            Subject.MoveEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IUpdateEpisodeFileService>()
                  .Verify(v => v.ChangeFileDateForFile(It.IsAny<EpisodeFile>(), It.IsAny<Series>(), It.IsAny<List<Episode>>()), Times.Once());
        }
    }
}
