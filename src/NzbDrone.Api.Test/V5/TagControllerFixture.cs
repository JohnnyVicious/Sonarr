using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Tags;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Tags;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class TagControllerFixture : TestBase<TagController>
    {
        private Tag _tag;

        [SetUp]
        public void Setup()
        {
            _tag = new Tag
            {
                Id = 1,
                Label = "test-tag"
            };

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("/api/v5/tag/1");

            Subject.Url = urlHelper.Object;
            Subject.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void should_get_all_tags()
        {
            var tags = new List<Tag>
            {
                new Tag { Id = 1, Label = "action" },
                new Tag { Id = 2, Label = "comedy" }
            };

            Mocker.GetMock<ITagService>()
                .Setup(s => s.All())
                .Returns(tags);

            var result = Subject.GetAll();

            result.Value.Should().HaveCount(2);
            result.Value[0].Label.Should().Be("action");
            result.Value[1].Label.Should().Be("comedy");
        }

        [Test]
        public void should_get_tag_by_id()
        {
            Mocker.GetMock<ITagService>()
                .Setup(s => s.GetTag(1))
                .Returns(_tag);

            var result = Subject.GetResourceByIdWithErrorHandler(1);

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<TagResource>>().Subject;
            okResult.Value.Id.Should().Be(1);
            okResult.Value.Label.Should().Be("test-tag");
        }

        [Test]
        public void should_delete_tag()
        {
            Subject.DeleteTag(1);

            Mocker.GetMock<ITagService>()
                .Verify(s => s.Delete(1), Times.Once());
        }

        [Test]
        public void should_return_empty_list_when_no_tags_exist()
        {
            Mocker.GetMock<ITagService>()
                .Setup(s => s.All())
                .Returns(new List<Tag>());

            var result = Subject.GetAll();

            result.Value.Should().BeEmpty();
        }

        [Test]
        public void should_update_tag()
        {
            var resource = new TagResource { Id = 1, Label = "updated-tag" };

            Mocker.GetMock<ITagService>()
                .Setup(s => s.Update(It.Is<Tag>(t => t.Label == "updated-tag")))
                .Returns(new Tag { Id = 1, Label = "updated-tag" });

            Mocker.GetMock<ITagService>()
                .Setup(s => s.GetTag(1))
                .Returns(new Tag { Id = 1, Label = "updated-tag" });

            Subject.Update(resource);

            Mocker.GetMock<ITagService>()
                .Verify(s => s.Update(It.Is<Tag>(t => t.Id == 1 && t.Label == "updated-tag")), Times.Once());
        }

        [Test]
        public void should_add_tag()
        {
            var resource = new TagResource { Label = "new-tag" };

            Mocker.GetMock<ITagService>()
                .Setup(s => s.Add(It.Is<Tag>(t => t.Label == "new-tag")))
                .Returns(new Tag { Id = 5, Label = "new-tag" });

            Mocker.GetMock<ITagService>()
                .Setup(s => s.GetTag(5))
                .Returns(new Tag { Id = 5, Label = "new-tag" });

            Subject.Create(resource);

            Mocker.GetMock<ITagService>()
                .Verify(s => s.Add(It.Is<Tag>(t => t.Label == "new-tag")), Times.Once());
        }

        [Test]
        public void should_get_all_returns_correct_resource_mapping()
        {
            var tags = new List<Tag>
            {
                new Tag { Id = 10, Label = "sci-fi" },
                new Tag { Id = 20, Label = "drama" },
                new Tag { Id = 30, Label = "horror" }
            };

            Mocker.GetMock<ITagService>()
                .Setup(s => s.All())
                .Returns(tags);

            var result = Subject.GetAll();

            result.Value.Should().HaveCount(3);
            result.Value.Should().Contain(r => r.Id == 10 && r.Label == "sci-fi");
            result.Value.Should().Contain(r => r.Id == 20 && r.Label == "drama");
            result.Value.Should().Contain(r => r.Id == 30 && r.Label == "horror");
        }
    }
}
