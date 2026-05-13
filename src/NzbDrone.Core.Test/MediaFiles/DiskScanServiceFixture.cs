using System.Collections.Generic; // NOSONAR
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class DiskScanServiceFixture : CoreTest<DiskScanService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Path = @"C:\Test\TV\Series".AsOsAgnostic())
                .With(s => s.Title = "Test Series")
                .Build();

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                  .Returns(@"C:\Test\TV".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(new string[0]);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFilesBySeries(_series.Id))
                  .Returns(new List<EpisodeFile>());
        }

        [Test]
        public void should_not_scan_if_root_folder_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(false);

            Subject.Scan(_series);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<SeriesScanSkippedEvent>(e => e.Reason == SeriesScanSkippedReason.RootFolderDoesNotExist)), Times.Once());
        }

        [Test]
        public void should_not_scan_if_root_folder_is_empty()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(true);

            Subject.Scan(_series);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<SeriesScanSkippedEvent>(e => e.Reason == SeriesScanSkippedReason.RootFolderIsEmpty)), Times.Once());
        }

        [Test]
        public void should_create_folder_if_series_folder_missing_and_create_empty_enabled()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(false);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CreateEmptySeriesFolders)
                  .Returns(true);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(false);

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Once());
        }

        [Test]
        public void should_not_create_folder_if_delete_empty_enabled()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(@"C:\Test\TV".AsOsAgnostic()))
                  .Returns(false);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.CreateEmptySeriesFolders)
                  .Returns(true);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(true);

            Subject.Scan(_series);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(_series.Path), Times.Never());
        }

        [Test]
        public void should_get_video_files_from_path()
        {
            var files = new[]
            {
                @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic(),
                @"C:\Test\TV\Series\episode.avi".AsOsAgnostic(),
                @"C:\Test\TV\Series\nfo.nfo".AsOsAgnostic()
            };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(@"C:\Test\TV\Series".AsOsAgnostic(), true))
                  .Returns(files);

            var result = Subject.GetVideoFiles(@"C:\Test\TV\Series".AsOsAgnostic());

            result.Should().HaveCount(2);
            result.Should().Contain(files[0]);
            result.Should().Contain(files[1]);
        }

        [Test]
        public void should_get_non_video_files_from_path()
        {
            var files = new[]
            {
                @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic(),
                @"C:\Test\TV\Series\subtitle.srt".AsOsAgnostic(),
                @"C:\Test\TV\Series\info.nfo".AsOsAgnostic()
            };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(@"C:\Test\TV\Series".AsOsAgnostic(), true))
                  .Returns(files);

            var result = Subject.GetNonVideoFiles(@"C:\Test\TV\Series".AsOsAgnostic());

            result.Should().HaveCount(2);
            result.Should().Contain(files[1]);
            result.Should().Contain(files[2]);
        }

        [Test]
        public void should_filter_excluded_subfolders()
        {
            var basePath = @"C:\Test\TV\Series".AsOsAgnostic();
            var paths = new List<string>
            {
                @"C:\Test\TV\Series\Season 1\episode.mkv".AsOsAgnostic(),
                @"C:\Test\TV\Series\extras\extra.mkv".AsOsAgnostic(),
                @"C:\Test\TV\Series\samples\sample.mkv".AsOsAgnostic()
            };

            var result = Subject.FilterPaths(basePath, paths);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_not_filter_extras_when_filter_extras_is_false()
        {
            var basePath = @"C:\Test\TV\Series".AsOsAgnostic();
            var paths = new List<string>
            {
                @"C:\Test\TV\Series\Season 1\episode.mkv".AsOsAgnostic(),
                @"C:\Test\TV\Series\extras\extra.mkv".AsOsAgnostic()
            };

            var result = Subject.FilterPaths(basePath, paths, filterExtras: false);

            result.Should().HaveCount(2);
        }

        [Test]
        public void should_execute_rescan_for_single_series()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(1))
                  .Returns(_series);

            Subject.Execute(new RescanSeriesCommand(1));

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Once());
        }

        [Test]
        public void should_execute_rescan_for_all_series()
        {
            var allSeries = new List<Series> { _series };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(allSeries);

            Subject.Execute(new RescanSeriesCommand());

            Mocker.GetMock<IMediaFileTableCleanupService>()
                  .Verify(v => v.Clean(It.IsAny<Series>(), It.IsAny<List<string>>()), Times.Once());
        }
    }
}
