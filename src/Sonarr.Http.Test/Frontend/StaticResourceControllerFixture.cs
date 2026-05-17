using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend;
using Sonarr.Http.Frontend.Mappers;

namespace Sonarr.Http.Test.Frontend
{
    [TestFixture]
    public class StaticResourceControllerFixture : TestBase
    {
        [TestCase("..%2findex.html")]
        [TestCase("..%5cindex.html")]
        [TestCase("../index.html")]
        [TestCase(@"..\index.html")]
        public async Task should_reject_path_traversal_before_mapping_resource(string path)
        {
            var mapper = Mocker.GetMock<IMapHttpRequestsToDisk>();
            var subject = new StaticResourceController(new[] { mapper.Object }, TestLogger);

            var result = await subject.IndexContent(path);

            result.Should().BeOfType<NotFoundResult>();

            mapper.Verify(v => v.CanHandle(It.IsAny<string>()), Times.Never());
        }
    }
}
