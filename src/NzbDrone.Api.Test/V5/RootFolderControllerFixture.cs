using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.RootFolders;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.RootFolders;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class RootFolderControllerFixture : TestBase<RootFolderController>
    {
        private RootFolder _rootFolder;

        [SetUp]
        public void Setup()
        {
            _rootFolder = new RootFolder
            {
                Id = 1,
                Path = "/tv",
                Accessible = true,
                FreeSpace = 100000,
                TotalSpace = 500000,
                UnmappedFolders = new List<UnmappedFolder>()
            };

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("/api/v5/rootfolder/1");

            Subject.Url = urlHelper.Object;
            Subject.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void should_get_all_root_folders()
        {
            var rootFolders = new List<RootFolder>
            {
                new RootFolder { Id = 1, Path = "/tv", Accessible = true, FreeSpace = 100000, TotalSpace = 500000, UnmappedFolders = new List<UnmappedFolder>() },
                new RootFolder { Id = 2, Path = "/anime", Accessible = true, FreeSpace = 200000, TotalSpace = 600000, UnmappedFolders = new List<UnmappedFolder>() }
            };

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.AllWithUnmappedFolders())
                .Returns(rootFolders);

            var result = Subject.GetRootFolders();

            result.Value.Should().HaveCount(2);
            result.Value[0].Path.Should().Be("/tv");
            result.Value[1].Path.Should().Be("/anime");
        }

        [Test]
        public void should_get_root_folder_by_id()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.Get(1, true))
                .Returns(_rootFolder);

            var result = Subject.GetResourceByIdWithErrorHandler(1);

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<RootFolderResource>>().Subject;
            okResult.Value.Id.Should().Be(1);
            okResult.Value.Path.Should().Be("/tv");
            okResult.Value.Accessible.Should().BeTrue();
        }

        [Test]
        public void should_delete_root_folder()
        {
            Subject.DeleteFolder(1);

            Mocker.GetMock<IRootFolderService>()
                .Verify(s => s.Remove(1), Times.Once());
        }

        [Test]
        public void should_add_root_folder()
        {
            var resource = new RootFolderResource { Path = "/movies" };

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.Add(It.Is<RootFolder>(r => r.Path == "/movies")))
                .Returns(new RootFolder { Id = 3, Path = "/movies", Accessible = true, UnmappedFolders = new List<UnmappedFolder>() });

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.Get(3, true))
                .Returns(new RootFolder { Id = 3, Path = "/movies", Accessible = true, UnmappedFolders = new List<UnmappedFolder>() });

            Subject.CreateRootFolder(resource);

            Mocker.GetMock<IRootFolderService>()
                .Verify(s => s.Add(It.Is<RootFolder>(r => r.Path == "/movies")), Times.Once());
        }

        [Test]
        public void should_return_empty_list_when_no_root_folders()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.AllWithUnmappedFolders())
                .Returns(new List<RootFolder>());

            var result = Subject.GetRootFolders();

            result.Value.Should().BeEmpty();
        }

        [Test]
        public void should_map_free_space_and_total_space()
        {
            var rootFolders = new List<RootFolder>
            {
                new RootFolder { Id = 1, Path = "/tv", Accessible = true, FreeSpace = 1024, TotalSpace = 4096, UnmappedFolders = new List<UnmappedFolder>() }
            };

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.AllWithUnmappedFolders())
                .Returns(rootFolders);

            var result = Subject.GetRootFolders();

            result.Value[0].FreeSpace.Should().Be(1024);
            result.Value[0].TotalSpace.Should().Be(4096);
        }

        [Test]
        public void should_map_accessible_flag()
        {
            var rootFolders = new List<RootFolder>
            {
                new RootFolder { Id = 1, Path = "/inaccessible", Accessible = false, UnmappedFolders = new List<UnmappedFolder>() }
            };

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.AllWithUnmappedFolders())
                .Returns(rootFolders);

            var result = Subject.GetRootFolders();

            result.Value[0].Accessible.Should().BeFalse();
        }
    }
}
