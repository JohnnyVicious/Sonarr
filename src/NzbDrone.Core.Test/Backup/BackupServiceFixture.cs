using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Backup
{
    [TestFixture]
    public class BackupServiceFixture : CoreTest<BackupService>
    {
        private string _backupFolder;
        private string _tempFolder;
        private string _appDataFolder;
        private string _configPath;
        private string _databaseRestore;

        [SetUp]
        public void Setup()
        {
            _tempFolder = @"C:\temp".AsOsAgnostic();
            _appDataFolder = @"C:\appdata".AsOsAgnostic();
            _backupFolder = Path.Combine(_appDataFolder, "Backups");

            // Extension methods GetConfigPath() and GetDatabaseRestore() use AppDataFolder
            _configPath = Path.Combine(_appDataFolder, "config.xml");
            _databaseRestore = Path.Combine(_appDataFolder, "sonarr.restore");

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(s => s.TempFolder)
                .Returns(_tempFolder);

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(s => s.AppDataFolder)
                .Returns(_appDataFolder);

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.BackupFolder)
                .Returns("Backups");

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.BackupRetention)
                .Returns(28);
        }

        private void GivenValidBackupFolder()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderWritable(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(It.IsAny<string>(), false))
                .Returns(Array.Empty<string>());

            Mocker.GetMock<IMainDatabase>()
                .SetupGet(s => s.DatabaseType)
                .Returns(DatabaseType.SQLite);
        }

        [Test]
        public void restore_should_extract_zip_and_move_config()
        {
            var backupFile = @"C:\backup\sonarr_backup.zip".AsOsAgnostic();
            var temporaryPath = Path.Combine(_tempFolder, "sonarr_backup_restore");
            var configFile = Path.Combine(temporaryPath, "Config.xml");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(temporaryPath, false))
                .Returns(new[] { configFile });

            Subject.Restore(backupFile);

            Mocker.GetMock<IArchiveService>()
                .Verify(s => s.Extract(backupFile, temporaryPath), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                .Verify(s => s.MoveFile(configFile, _configPath, true), Times.Once());
        }

        [Test]
        public void restore_should_extract_zip_and_move_sonarr_db()
        {
            var backupFile = @"C:\backup\sonarr_backup.zip".AsOsAgnostic();
            var temporaryPath = Path.Combine(_tempFolder, "sonarr_backup_restore");
            var dbFile = Path.Combine(temporaryPath, "sonarr.db");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(temporaryPath, false))
                .Returns(new[] { dbFile });

            Subject.Restore(backupFile);

            Mocker.GetMock<IDiskProvider>()
                .Verify(s => s.MoveFile(dbFile, _databaseRestore, true), Times.Once());
        }

        [Test]
        public void restore_should_extract_zip_and_move_nzbdrone_db()
        {
            var backupFile = @"C:\backup\sonarr_backup.zip".AsOsAgnostic();
            var temporaryPath = Path.Combine(_tempFolder, "sonarr_backup_restore");
            var dbFile = Path.Combine(temporaryPath, "nzbdrone.db");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(temporaryPath, false))
                .Returns(new[] { dbFile });

            Subject.Restore(backupFile);

            Mocker.GetMock<IDiskProvider>()
                .Verify(s => s.MoveFile(dbFile, _databaseRestore, true), Times.Once());
        }

        [Test]
        public void restore_zip_with_no_recognized_files_should_throw()
        {
            var backupFile = @"C:\backup\sonarr_backup.zip".AsOsAgnostic();
            var temporaryPath = Path.Combine(_tempFolder, "sonarr_backup_restore");
            var unknownFile = Path.Combine(temporaryPath, "unknown.txt");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(temporaryPath, false))
                .Returns(new[] { unknownFile });

            Assert.Throws<RestoreBackupFailedException>(() => Subject.Restore(backupFile));
        }

        [Test]
        public void restore_db_file_should_move_directly()
        {
            var backupFile = @"C:\backup\sonarr.db".AsOsAgnostic();

            Subject.Restore(backupFile);

            Mocker.GetMock<IDiskProvider>()
                .Verify(s => s.MoveFile(backupFile, _databaseRestore, true), Times.Once());

            Mocker.GetMock<IArchiveService>()
                .Verify(s => s.Extract(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void restore_zip_should_cleanup_temp_directory()
        {
            var backupFile = @"C:\backup\sonarr_backup.zip".AsOsAgnostic();
            var temporaryPath = Path.Combine(_tempFolder, "sonarr_backup_restore");
            var configFile = Path.Combine(temporaryPath, "Config.xml");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(temporaryPath, false))
                .Returns(new[] { configFile });

            Subject.Restore(backupFile);

            Mocker.GetMock<IDiskProvider>()
                .Verify(s => s.DeleteFolder(temporaryPath, true), Times.Once());
        }

        [Test]
        public void backup_should_throw_when_folder_is_not_writable()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderWritable(It.IsAny<string>()))
                .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(It.IsAny<string>(), false))
                .Returns(Array.Empty<string>());

            Assert.Throws<UnauthorizedAccessException>(() => Subject.Backup(BackupType.Manual));
        }

        [Test]
        public void backup_should_call_backup_database()
        {
            GivenValidBackupFolder();

            Subject.Backup(BackupType.Manual);

            Mocker.GetMock<IMakeDatabaseBackup>()
                .Verify(s => s.BackupDatabase(It.IsAny<IMainDatabase>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void backup_should_transfer_config_file()
        {
            GivenValidBackupFolder();

            Subject.Backup(BackupType.Manual);

            Mocker.GetMock<IDiskTransferService>()
                .Verify(s => s.TransferFile(_configPath, It.IsAny<string>(), TransferMode.Copy), Times.Once());
        }

        [Test]
        public void backup_should_create_zip_in_correct_folder()
        {
            GivenValidBackupFolder();

            Subject.Backup(BackupType.Manual);

            var expectedFolder = Path.Combine(_backupFolder, "manual");

            Mocker.GetMock<IArchiveService>()
                .Verify(s => s.CreateZip(It.Is<string>(p => p.StartsWith(expectedFolder)), It.IsAny<string[]>()), Times.Once());
        }

        [Test]
        public void get_backup_folder_should_return_absolute_path_when_rooted()
        {
            var absolutePath = @"C:\custom\backups".AsOsAgnostic();

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.BackupFolder)
                .Returns(absolutePath);

            var result = Subject.GetBackupFolder();

            result.Should().Be(absolutePath);
        }

        [Test]
        public void get_backup_folder_should_combine_with_appdata_when_relative()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.BackupFolder)
                .Returns("Backups");

            var result = Subject.GetBackupFolder();

            result.Should().Be(Path.Combine(_appDataFolder, "Backups"));
        }

        [Test]
        public void get_backup_folder_with_type_should_append_type_subfolder()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.BackupFolder)
                .Returns("Backups");

            var result = Subject.GetBackupFolder(BackupType.Manual);

            result.Should().Be(Path.Combine(_appDataFolder, "Backups", "manual"));
        }

        [Test]
        public void get_backups_should_return_backups_from_existing_folders()
        {
            var manualFolder = Path.Combine(_backupFolder, "manual");
            var backupFileName = "sonarr_backup_v4.0.0_2024.01.01_12.00.00.zip";
            var backupFilePath = Path.Combine(manualFolder, backupFileName);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(manualFolder))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(manualFolder, false))
                .Returns(new[] { backupFilePath });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(backupFilePath))
                .Returns(1024);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileGetLastWrite(backupFilePath))
                .Returns(new DateTime(2024, 1, 1));

            var result = Subject.GetBackups();

            result.Should().HaveCountGreaterOrEqualTo(1);
            result.Should().Contain(b => b.Name == backupFileName);
        }

        [Test]
        public void get_backups_should_filter_by_backup_file_regex()
        {
            var manualFolder = Path.Combine(_backupFolder, "manual");
            var validFile = Path.Combine(manualFolder, "sonarr_backup_v4.0.0_2024.01.01_12.00.00.zip");
            var invalidFile = Path.Combine(manualFolder, "not_a_backup.txt");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(manualFolder))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(manualFolder, false))
                .Returns(new[] { validFile, invalidFile });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(It.IsAny<string>()))
                .Returns(1024);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileGetLastWrite(It.IsAny<string>()))
                .Returns(new DateTime(2024, 1, 1));

            var result = Subject.GetBackups();

            result.Should().OnlyContain(b => b.Name.Contains("sonarr_backup"));
        }

        [TestCase("sonarr_backup_v4.0.0_2024.01.01_12.00.00.zip", true)]
        [TestCase("nzbdrone_backup_v3.0.0_2024.01.01_12.00.00.zip", true)]
        [TestCase("sonarr_backup_2024.01.01_12.00.00.zip", true)]
        [TestCase("not_a_backup.zip", false)]
        [TestCase("sonarr_backup_v4.0.0_2024.01.01_12.00.00.txt", false)]
        public void backup_file_regex_should_match_correctly(string fileName, bool shouldMatch)
        {
            BackupService.BackupFileRegex.IsMatch(fileName).Should().Be(shouldMatch);
        }
    }
}
