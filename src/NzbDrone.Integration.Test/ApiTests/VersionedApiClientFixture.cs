using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Integration.Test.Client;
using RestSharp;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class VersionedApiClientFixture : IntegrationTest
    {
        [Test]
        public void should_call_v3_and_v5_from_the_same_fixture()
        {
            var v3Response = ApiV3.Get("system/status");
            var v5Response = ApiV5.Get("system/status");

            v3Response.ShouldHaveJsonObjectContent().ContainsKey("version").Should().BeTrue();
            v5Response.ShouldHaveJsonObjectContent().ContainsKey("version").Should().BeTrue();
        }

        [Test]
        public void should_support_unauthenticated_request_helpers()
        {
            var response = ApiV3.Get("system/status", authenticated: false);

            response.ShouldHaveJsonObjectContent().ContainsKey("version").Should().BeTrue();
        }

        [Test]
        public void should_load_openapi_specs_for_versioned_response_assertions()
        {
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
        }
    }
}
