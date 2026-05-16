using System.Net;
using FluentAssertions;
using NUnit.Framework;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    [TestFixture]
    public class VersionedApiClientInfrastructureFixture
    {
        [Test]
        public void should_build_explicit_v3_and_v5_api_roots()
        {
            var apiV3 = new VersionedApiClient("http://localhost:8989/", "v3", "test-key");
            var apiV5 = new VersionedApiClient("http://localhost:8989/", "v5", "test-key");

            apiV3.AuthenticatedRestClient.BuildUri(apiV3.BuildRequest("system/status")).ToString()
                .Should().Be("http://localhost:8989/api/v3/system/status");
            apiV5.AuthenticatedRestClient.BuildUri(apiV5.BuildRequest("system/status")).ToString()
                .Should().Be("http://localhost:8989/api/v5/system/status");
        }

        [Test]
        public void should_keep_unauthenticated_requests_on_the_same_versioned_root()
        {
            var apiV5 = new VersionedApiClient("http://localhost:8989/", "v5", "test-key");
            var request = apiV5.BuildRequest("system/status");

            apiV5.UnauthenticatedRestClient.BuildUri(request).ToString()
                .Should().Be("http://localhost:8989/api/v5/system/status");
        }

        [Test]
        public void should_load_checked_in_openapi_specs_for_response_assertions()
        {
            var apiV3 = new VersionedApiClient("http://localhost:8989/", "v3", "test-key");
            var apiV5 = new VersionedApiClient("http://localhost:8989/", "v5", "test-key");

            apiV3.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            apiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            apiV5.OpenApi.GetResponseSchema(Method.GET, "system/status", HttpStatusCode.OK)
                .ContainsKey("$ref").Should().BeTrue();
        }
    }
}
