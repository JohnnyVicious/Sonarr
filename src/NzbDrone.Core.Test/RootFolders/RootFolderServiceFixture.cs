using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.RootFolders
{
    [TestFixture]
    public class RootFolderServiceFixture : CoreTest<RootFolderService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ICacheManager>()
                  .Setup(s => s.GetCache<string>(It.IsAny<Type>()))
                  .Returns(new Cached<string>());

            Mocker.GetMock<IRootFolderRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<RootFolder>());
        }

        private void GivenExistingRootFolder(string path)
        {
            Mocker.GetMock<IRootFolderRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<RootFolder> { new RootFolder { Path = path } });
        }

        [Test]
        public void should_throw_if_path_is_null_or_empty()
        {
            var rootFolder = new RootFolder { Path = "" };

            Assert.Throws<ArgumentException>(() => Subject.Add(rootFolder));
        }

        [Test]
        public void should_throw_if_path_is_not_rooted()
        {
            var rootFolder = new RootFolder { Path = "relative/path" };

            Assert.Throws<ArgumentException>(() => Subject.Add(rootFolder));
        }

        [Test]
        public void should_throw_if_path_does_not_exist()
        {
            var rootFolder = new RootFolder { Path = @"C:\NonExistent".AsOsAgnostic() };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(rootFolder.Path))
                  .Returns(false);

            Assert.Throws<DirectoryNotFoundException>(() => Subject.Add(rootFolder));
        }

        [Test]
        public void should_throw_if_path_already_exists_in_database()
        {
            var path = @"C:\TV".AsOsAgnostic();

            GivenExistingRootFolder(path);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(path))
                  .Returns(true);

            var rootFolder = new RootFolder { Path = path };

            Assert.Throws<InvalidOperationException>(() => Subject.Add(rootFolder));
        }

        [Test]
        public void should_throw_if_path_is_not_writable()
        {
            var path = @"C:\TV".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(path))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderWritable(path))
                  .Returns(false);

            var rootFolder = new RootFolder { Path = path };

            Assert.Throws<UnauthorizedAccessException>(() => Subject.Add(rootFolder));
        }

        [Test]
        public void should_return_all_root_folders()
        {
            var rootFolders = new List<RootFolder>
            {
                new RootFolder { Id = 1, Path = @"C:\TV".AsOsAgnostic() },
                new RootFolder { Id = 2, Path = @"C:\TV2".AsOsAgnostic() }
            };

            Mocker.GetMock<IRootFolderRepository>()
                  .Setup(s => s.All())
                  .Returns(rootFolders);

            Subject.All().Should().HaveCount(2);
        }

        [Test]
        public void should_remove_root_folder()
        {
            Subject.Remove(1);

            Mocker.GetMock<IRootFolderRepository>()
                  .Verify(v => v.Delete(1), Times.Once());
        }

        [Test]
        public void should_get_root_folder_by_id()
        {
            var rootFolder = new RootFolder { Id = 1, Path = @"C:\TV".AsOsAgnostic() };

            Mocker.GetMock<IRootFolderRepository>()
                  .Setup(s => s.Get(1))
                  .Returns(rootFolder);

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.AllSeriesPaths())
                  .Returns(new Dictionary<int, string>());

            var result = Subject.Get(1, false);

            result.Path.Should().Be(rootFolder.Path);
        }
    }
}
