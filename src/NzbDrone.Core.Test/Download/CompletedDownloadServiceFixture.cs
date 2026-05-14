using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class CompletedDownloadServiceFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                .With(h => h.Status = DownloadItemStatus.Completed)
                .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                .With(h => h.Title = "Drone.S01E01.HDTV")
                .With(h => h.Category = "tv")
                .Build();

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series { Id = 1 },
                Episodes = new List<Episode> { new Episode { Id = 1 } }
            };

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                .With(c => c.State = TrackedDownloadState.Downloading)
                .With(c => c.DownloadItem = completed)
                .With(c => c.RemoteEpisode = remoteEpisode)
                .With(c => c.ImportItem = completed)
                .Build();

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(c => c.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns((DownloadClientItem item, DownloadClientItem previous) => item);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                  .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetSeries("Drone.S01E01.HDTV"))
                  .Returns(remoteEpisode.Series);
        }

        [Test]
        public void should_skip_if_download_is_not_completed()
        {
            _trackedDownload.DownloadItem.Status = DownloadItemStatus.Downloading;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
        }

        [Test]
        public void should_skip_if_state_is_not_downloading_or_import_blocked()
        {
            _trackedDownload.State = TrackedDownloadState.Imported;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
        }

        [Test]
        public void should_set_state_to_import_pending_when_series_found()
        {
            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
        }

        [Test]
        public void should_warn_when_no_history_and_no_category()
        {
            _trackedDownload.DownloadItem.Category = null;

            Subject.Check(_trackedDownload);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void should_warn_when_remote_episode_is_null_on_import()
        {
            _trackedDownload.RemoteEpisode = null;

            Subject.Import(_trackedDownload);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void should_set_state_to_importing_during_import()
        {
            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(s => s.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Callback<string, ImportMode, Series, DownloadClientItem>((_, _, _, _) => _trackedDownload.State.Should().Be(TrackedDownloadState.Importing))
                  .Returns(new List<ImportResult>());

            Subject.Import(_trackedDownload);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void should_verify_import_returns_true_when_all_episodes_imported()
        {
            var localEpisode = new LocalEpisode
            {
                Episodes = new List<Episode> { new Episode { Id = 1 } }
            };

            // ImportResult with no errors has Result == Imported
            var importResults = new List<ImportResult>
            {
                new ImportResult(new ImportDecision(localEpisode), new EpisodeFile())
            };

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Subject.VerifyImport(_trackedDownload, importResults).Should().BeTrue();
            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
        }

        [Test]
        public void should_return_false_when_not_all_episodes_imported()
        {
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode>
            {
                new Episode { Id = 1 },
                new Episode { Id = 2 }
            };

            var localEpisode = new LocalEpisode
            {
                Episodes = new List<Episode> { new Episode { Id = 1 } }
            };

            // ImportResult with no errors has Result == Imported
            var importResults = new List<ImportResult>
            {
                new ImportResult(new ImportDecision(localEpisode), new EpisodeFile())
            };

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<EpisodeHistory>>()))
                  .Returns(false);

            Subject.VerifyImport(_trackedDownload, importResults).Should().BeFalse();
        }

        [Test]
        public void should_check_from_import_blocked_state()
        {
            _trackedDownload.State = TrackedDownloadState.ImportBlocked;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
        }
    }
}
