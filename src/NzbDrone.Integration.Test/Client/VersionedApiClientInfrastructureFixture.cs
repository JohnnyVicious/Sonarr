using System;
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
            var apiV3 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v3", "test-key");
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");

            apiV3.AuthenticatedRestClient.BuildUri(apiV3.BuildRequest("system/status")).ToString()
                .Should().Be("http://localhost:8989/api/v3/system/status");
            apiV5.AuthenticatedRestClient.BuildUri(apiV5.BuildRequest("system/status")).ToString()
                .Should().Be("http://localhost:8989/api/v5/system/status");
        }

        [Test]
        public void should_preserve_supplied_loopback_authority()
        {
            var apiV5 = new VersionedApiClient(new Uri("http://127.0.0.1:8989/"), "v5", "test-key");

            apiV5.BuildUri(apiV5.BuildRequest("system/status")).ToString()
                .Should().Be("http://127.0.0.1:8989/api/v5/system/status");
        }

        [Test]
        public void should_reject_non_loopback_api_roots()
        {
            var createClient = () => new VersionedApiClient(new Uri("https://example.com/"), "v5", "test-key");

            createClient.Should().Throw<ArgumentException>();
        }

        [Test]
        public void should_keep_unauthenticated_requests_on_the_same_versioned_root()
        {
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");
            var request = apiV5.BuildRequest("system/status");

            apiV5.UnauthenticatedRestClient.BuildUri(request).ToString()
                .Should().Be("http://localhost:8989/api/v5/system/status");
            apiV5.UnauthenticatedRestClient.DefaultParameters
                .Should().NotContain(parameter => parameter.Name == "Authorization" || parameter.Name == "X-Api-Key");
            apiV5.AuthenticatedRestClient.DefaultParameters
                .Should().Contain(parameter => parameter.Name == "Authorization")
                .And.Contain(parameter => parameter.Name == "X-Api-Key");
        }

        [Test]
        public void should_build_feed_requests_against_the_versioned_feed_root()
        {
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");

            apiV5.BuildUri(apiV5.BuildRequest("feed/calendar/sonarr.ics")).ToString()
                .Should().Be("http://localhost:8989/feed/v5/calendar/sonarr.ics");
            apiV5.BuildUri(apiV5.BuildRequest("/feed/v5/calendar/sonarr.ics")).ToString()
                .Should().Be("http://localhost:8989/feed/v5/calendar/sonarr.ics");
        }

        [Test]
        public void should_not_mutate_feed_requests_when_building_uris()
        {
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");
            var request = apiV5.BuildRequest("feed/calendar/sonarr.ics");

            apiV5.BuildUri(request).ToString()
                .Should().Be("http://localhost:8989/feed/v5/calendar/sonarr.ics");
            request.Resource.Should().Be("feed/calendar/sonarr.ics");
            apiV5.BuildUri(request).ToString()
                .Should().Be("http://localhost:8989/feed/v5/calendar/sonarr.ics");
        }

        [Test]
        public void should_load_checked_in_openapi_specs_for_response_assertions()
        {
            var apiV3 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v3", "test-key");
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");

            apiV3.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            apiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            apiV5.OpenApi.GetResponseSchema(Method.GET, "system/status", HttpStatusCode.OK)
                .ContainsKey("$ref").Should().BeTrue();
        }

        [Test]
        public void should_normalize_relative_and_absolute_api_paths_for_openapi_assertions()
        {
            var apiV5 = new VersionedApiClient(new Uri("http://localhost:8989/"), "v5", "test-key");

            apiV5.OpenApi.ApiPath("system/status").Should().Be("/api/v5/system/status");
            apiV5.OpenApi.ApiPath("/system/status").Should().Be("/api/v5/system/status");
            apiV5.OpenApi.ApiPath("/api/v5/system/status").Should().Be("/api/v5/system/status");
            apiV5.OpenApi.ApiPath("feed/calendar/sonarr.ics").Should().Be("/feed/v5/calendar/sonarr.ics");
            apiV5.OpenApi.ApiPath("/feed/v5/calendar/sonarr.ics").Should().Be("/feed/v5/calendar/sonarr.ics");
        }

        [Test]
        public void should_parse_json_array_responses()
        {
            var response = new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json; charset=utf-8",
                Content = "[{\"id\":1}]"
            };

            response.ShouldHaveJsonArrayContent().Count.Should().Be(1);
        }

        [Test]
        public void should_validate_bad_request_validation_error_shapes()
        {
            var response = new RestResponse
            {
                StatusCode = HttpStatusCode.BadRequest,
                ContentType = "application/json; charset=utf-8",
                Content = "[{\"propertyName\":\"path\",\"errorMessage\":\"Path is required\"}]"
            };

            response.ShouldHaveValidationErrors().Count.Should().Be(1);
        }

        [Test]
        public void should_reject_malformed_validation_error_shapes()
        {
            var response = new RestResponse
            {
                StatusCode = HttpStatusCode.BadRequest,
                ContentType = "application/json; charset=utf-8",
                Content = "[{\"propertyName\":\"path\"}]"
            };

            var validation = () => response.ShouldHaveValidationErrors();

            validation.Should().Throw<AssertionException>();
        }
    }
}
