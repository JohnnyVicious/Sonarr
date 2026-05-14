using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MediaFileDeletionServiceFixture : CoreTest<NzbDrone.Core.MediaFiles.MediaFileDeletionService>
    {
        private Series _series;
        private EpisodeFile _episodeFile;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.Path = @"C:\Test\TV\Series".AsOsAgnostic())
                .Build();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Id = 1)
                .With(e => e.RelativePath = @"Season 1\episode.mkv")
                .Build();

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                  .Returns(@"C:\Test\TV".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(It.IsAny<string>()))
                  .Returns(new string[] { @"C:\Test\TV\Series".AsOsAgnostic() });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetParentFolder(It.IsAny<string>()))
                  .Returns(@"C:\Test\TV".AsOsAgnostic());
        }

        [Test]
        public void should_throw_if_root_folder_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(false);

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteEpisodeFile(_series, _episodeFile));
        }

        [Test]
        public void should_throw_if_root_folder_is_empty()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(new string[0]);

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteEpisodeFile(_series, _episodeFile));
        }

        [Test]
        public void should_delete_from_database_even_if_file_not_on_disk()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Subject.DeleteEpisodeFile(_series, _episodeFile);

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Delete(_episodeFile, DeleteMediaFileReason.Manual), Times.Once());
        }

        [Test]
        public void should_not_delete_series_folder_when_other_series_has_same_path()
        {
            var otherSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = 2)
                .With(s => s.Path = _series.Path)
                .Build();

            var allSeriesPaths = new Dictionary<int, string>
            {
                { 1, _series.Path },
                { 2, otherSeries.Path }
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(allSeriesPaths);

            var message = new SeriesDeletedEvent(new List<Series> { _series }, true, false);

            Subject.HandleAsync(message);

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_delete_series_folder_on_series_deleted()
        {
            var allSeriesPaths = new Dictionary<int, string>
            {
                { 1, _series.Path }
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(allSeriesPaths);

            var message = new SeriesDeletedEvent(new List<Series> { _series }, true, false);

            Subject.HandleAsync(message);

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(v => v.DeleteFolder(_series.Path), Times.Once());
        }

        [Test]
        public void should_not_delete_files_on_series_deleted_when_flag_is_false()
        {
            var message = new SeriesDeletedEvent(new List<Series> { _series }, false, false);

            Subject.HandleAsync(message);

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_handle_episode_file_deleted_event_and_cleanup_empty_folders()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Path = @"C:\Test\TV\Series\Season 1\episode.mkv".AsOsAgnostic())
                .With(e => e.Series = new NzbDrone.Core.Datastore.LazyLoaded<Series>(_series))
                .Build();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(It.IsAny<string>()))
                  .Returns(false);

            Subject.Handle(new EpisodeFileDeletedEvent(episodeFile, DeleteMediaFileReason.Manual));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.RemoveEmptySubfolders(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Test]
        public void should_not_cleanup_when_delete_empty_folders_is_disabled()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Path = @"C:\Test\TV\Series\Season 1\episode.mkv".AsOsAgnostic())
                .With(e => e.Series = new NzbDrone.Core.Datastore.LazyLoaded<Series>(_series))
                .Build();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(false);

            Subject.Handle(new EpisodeFileDeletedEvent(episodeFile, DeleteMediaFileReason.Manual));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.RemoveEmptySubfolders(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_cleanup_when_reason_is_missing_from_disk()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(e => e.Path = @"C:\Test\TV\Series\Season 1\episode.mkv".AsOsAgnostic())
                .With(e => e.Series = new NzbDrone.Core.Datastore.LazyLoaded<Series>(_series))
                .Build();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(true);

            Subject.Handle(new EpisodeFileDeletedEvent(episodeFile, DeleteMediaFileReason.MissingFromDisk));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.RemoveEmptySubfolders(It.IsAny<string>()), Times.Never());
        }
    }
}
