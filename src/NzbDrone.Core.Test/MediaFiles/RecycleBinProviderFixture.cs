using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class RecycleBinProviderFixture : CoreTest<RecycleBinProvider>
    {
        private string _recycleBinPath;

        [SetUp]
        public void Setup()
        {
            _recycleBinPath = @"C:\Test\RecycleBin".AsOsAgnostic();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBin)
                  .Returns(_recycleBinPath);
        }

        [Test]
        public void should_delete_folder_permanently_when_recycle_bin_not_configured()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBin)
                  .Returns(string.Empty);

            var path = @"C:\Test\TV\Series".AsOsAgnostic();

            Subject.DeleteFolder(path);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(path, true), Times.Once());
        }

        [Test]
        public void should_move_folder_to_recycle_bin_when_configured()
        {
            var path = @"C:\Test\TV\Series".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                  .Returns(new string[0]);

            Subject.DeleteFolder(path);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFolder(path, It.IsAny<string>(), TransferMode.Move), Times.Once());
        }

        [Test]
        public void should_delete_file_permanently_when_recycle_bin_not_configured()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBin)
                  .Returns(string.Empty);

            var path = @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic();

            var result = Subject.DeleteFile(path);

            result.Should().BeNull();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFile(path), Times.Once());
        }

        [Test]
        public void should_move_file_to_recycle_bin_when_configured()
        {
            var path = @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Subject.DeleteFile(path);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(path, It.IsAny<string>(), TransferMode.Move), Times.Once());
        }

        [Test]
        public void should_append_index_when_file_already_exists_in_recycle_bin()
        {
            var path = @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .SetupSequence(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true)
                  .Returns(false);

            Subject.DeleteFile(path);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(v => v.TransferFile(path, It.Is<string>(s => s.Contains("_2")), TransferMode.Move), Times.Once());
        }

        [Test]
        public void should_move_file_with_subfolder()
        {
            var path = @"C:\Test\TV\Series\episode.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Subject.DeleteFile(path, "Series");

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.CreateFolder(It.Is<string>(s => s.Contains("Series"))), Times.Once());
        }

        [Test]
        public void should_not_empty_when_recycle_bin_not_configured()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBin)
                  .Returns(string.Empty);

            Subject.Empty();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetDirectories(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_empty_recycle_bin()
        {
            var folders = new[] { @"C:\Test\RecycleBin\Series1".AsOsAgnostic() };
            var files = new[] { @"C:\Test\RecycleBin\file.txt".AsOsAgnostic() };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(_recycleBinPath))
                  .Returns(folders);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(_recycleBinPath, false))
                  .Returns(files);

            Subject.Empty();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(folders[0], true), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFile(files[0]), Times.Once());
        }

        [Test]
        public void should_not_cleanup_when_recycle_bin_not_configured()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBin)
                  .Returns(string.Empty);

            Subject.Cleanup();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_not_cleanup_when_cleanup_days_is_zero()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBinCleanupDays)
                  .Returns(0);

            Subject.Cleanup();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_delete_expired_files_during_cleanup()
        {
            var expiredFile = @"C:\Test\RecycleBin\old.mkv".AsOsAgnostic();
            var recentFile = @"C:\Test\RecycleBin\new.mkv".AsOsAgnostic();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RecycleBinCleanupDays)
                  .Returns(7);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(_recycleBinPath, true))
                  .Returns(new[] { expiredFile, recentFile });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileGetLastWrite(expiredFile))
                  .Returns(DateTime.UtcNow.AddDays(-10));

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileGetLastWrite(recentFile))
                  .Returns(DateTime.UtcNow.AddDays(-1));

            Subject.Cleanup();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFile(expiredFile), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFile(recentFile), Times.Never());
        }
    }
}
