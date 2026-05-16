using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using Sonarr.Api.V5;
using Sonarr.Http.ClientSchema;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using V3IndexerBulkResource = Sonarr.Api.V3.Indexers.IndexerBulkResource;
using V3IndexerResource = Sonarr.Api.V3.Indexers.IndexerResource;
using V3ReleaseProfileResource = Sonarr.Api.V3.Profiles.Release.ReleaseProfileResource;
using V3ReleaseResource = Sonarr.Api.V3.Indexers.ReleaseResource;
using V5IndexerBulkResource = Sonarr.Api.V5.Indexers.IndexerBulkResource;
using V5IndexerFlagResource = Sonarr.Api.V5.Indexers.IndexerFlagResource;
using V5IndexerResource = Sonarr.Api.V5.Indexers.IndexerResource;
using V5ReleaseGrabResource = Sonarr.Api.V5.Release.ReleaseGrabResource;
using V5ReleaseProfileResource = Sonarr.Api.V5.Profiles.Release.ReleaseProfileResource;
using V5ReleasePushResource = Sonarr.Api.V5.Release.ReleasePushResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class IndexerReleaseV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_indexer_release_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "indexer", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "indexer", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "indexer", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "indexer/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "indexer/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "indexer/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "indexer/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "indexer/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "indexer/bulk", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "indexer/bulk", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "indexer/bulk", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "indexer/schema", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "indexer/test", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "indexer/testall", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "indexer/testall", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareOperation(Method.POST, "indexer/action/{name}");
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "indexerflag", HttpStatusCode.OK);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "release", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "release", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "release", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "release", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "release/push", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "release/push", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "release/push/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "release/push/{id}", HttpStatusCode.NotFound);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "releaseprofile", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "releaseprofile", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "releaseprofile", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "releaseprofile/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "releaseprofile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "releaseprofile/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "releaseprofile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "releaseprofile/{id}", HttpStatusCode.NoContent);

            foreach (var resource in new[] { "indexer", "release", "releaseprofile" })
            {
                ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, resource, HttpStatusCode.OK);
                ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, resource);
            }

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "indexer/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "indexer/{id}");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, "indexer/{id}");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "indexer/bulk");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, "indexer/bulk");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "indexer/schema", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "indexer/test");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "indexer/testall");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "indexer/action/{name}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "indexerflag", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "release/push");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "releaseprofile/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "releaseprofile/{id}");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, "releaseprofile/{id}");
        }

        [Test]
        public void should_reject_unauthenticated_indexer_release_requests()
        {
            ShouldRejectUnauthenticatedProviderRequests(ApiV5, "indexer", new V5IndexerResource());
            ExecuteUnauthenticated(ApiV5, "indexer/bulk", Method.PUT, new V5IndexerBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV5, "indexer/bulk", Method.DELETE, new V5IndexerBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV5, "indexerflag", Method.GET);

            ExecuteUnauthenticated(ApiV5, "release", Method.GET);
            ExecuteUnauthenticated(ApiV5, "release", Method.POST, new V5ReleaseGrabResource { Guid = "missing", IndexerId = 1 });
            ExecuteUnauthenticated(ApiV5, "release/push", Method.POST, new V5ReleasePushResource());

            ExecuteUnauthenticated(ApiV5, "releaseprofile", Method.GET);
            ExecuteUnauthenticated(ApiV5, "releaseprofile/1", Method.GET);
            ExecuteUnauthenticated(ApiV5, "releaseprofile", Method.POST, new V5ReleaseProfileResource());
            ExecuteUnauthenticated(ApiV5, "releaseprofile/1", Method.PUT, new V5ReleaseProfileResource { Id = 1 });
            ExecuteUnauthenticated(ApiV5, "releaseprofile/1", Method.DELETE);

            ShouldRejectUnauthenticatedProviderRequests(ApiV3, "indexer", new V3IndexerResource());
            ExecuteUnauthenticated(ApiV3, "indexer/bulk", Method.PUT, new V3IndexerBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV3, "indexer/bulk", Method.DELETE, new V3IndexerBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV3, "release", Method.GET);
            ExecuteUnauthenticated(ApiV3, "release", Method.POST, new V3ReleaseResource { Guid = "missing", IndexerId = 1 });
            ExecuteUnauthenticated(ApiV3, "release/push", Method.POST, new V3ReleaseResource());
            ExecuteUnauthenticated(ApiV3, "releaseprofile", Method.GET);
            ExecuteUnauthenticated(ApiV3, "releaseprofile/1", Method.GET);
            ExecuteUnauthenticated(ApiV3, "releaseprofile", Method.POST, new V3ReleaseProfileResource());
            ExecuteUnauthenticated(ApiV3, "releaseprofile/1", Method.PUT, new V3ReleaseProfileResource { Id = 1 });
            ExecuteUnauthenticated(ApiV3, "releaseprofile/1", Method.DELETE);
        }

        [Test]
        public void should_return_indexer_schema_flags_and_action_results()
        {
            using var server = NewznabTestServer.Start();
            var schema = BuildV5NewznabIndexer(server.RootUrl);

            var schemas = GetV5IndexerSchema();
            var newznab = schemas.Single(indexer => indexer.Implementation == "Newznab");

            newznab.ConfigContract.Should().Be("NewznabSettings");
            newznab.Protocol.Should().Be(DownloadProtocol.Usenet);
            newznab.Fields.Should().Contain(field => field.Name == "baseUrl");
            newznab.Fields.Should().Contain(field => field.Name == "categories");

            var flags = Read<List<V5IndexerFlagResource>>(ApiV5.Get("indexerflag"));
            flags.Should().OnlyContain(flag => flag.Id > 0);
            flags.Should().Contain(flag => flag.Name == "Scene" && flag.NameLower == "scene");

            var action = ApiV5.Post("indexer/action/newznabCategories", schema, HttpStatusCode.OK).ShouldHaveJsonObjectContent();
            action.ContainsKey("options").Should().BeTrue();
        }

        [Test]
        public void should_create_update_bulk_test_and_delete_v5_indexer_against_mock_newznab()
        {
            using var server = NewznabTestServer.Start();
            var createdId = 0;

            try
            {
                var payload = BuildV5NewznabIndexer(server.RootUrl, enableRss: true);
                var created = Read<V5IndexerResource>(ApiV5.Post("indexer?skipTesting=true", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);
                created.EnableRss.Should().BeTrue();
                GetV5Indexers().Should().Contain(indexer => indexer.Id == created.Id && indexer.Name == payload.Name);

                var byId = GetV5Indexer(created.Id);
                SetNewznabSettings(byId.Fields, server.RootUrl);
                byId.Name = TestData.NextName("indexer-renamed");
                byId.EnableInteractiveSearch = true;
                byId.Priority = 30;

                var updated = Read<V5IndexerResource>(ApiV5.Put($"indexer/{byId.Id}?skipTesting=true", byId, HttpStatusCode.Accepted));
                updated.Name.Should().Be(byId.Name);
                updated.EnableInteractiveSearch.Should().BeTrue();
                GetV5Indexer(created.Id).Name.Should().Be(byId.Name);

                var bulkResult = Read<List<V5IndexerResource>>(PutV5IndexerBulk(new V5IndexerBulkResource
                {
                    Ids = new List<int> { created.Id },
                    Tags = new List<int> { TestData.Tag().Id },
                    ApplyTags = ApplyTags.Replace,
                    EnableAutomaticSearch = true,
                    SeasonSearchMaximumSingleEpisodeAge = 7,
                    Priority = 35
                }));

                var bulkUpdated = bulkResult.Single(indexer => indexer.Id == created.Id);
                bulkUpdated.EnableAutomaticSearch.Should().BeTrue();
                bulkUpdated.Priority.Should().Be(35);
                bulkUpdated.Tags.Should().NotBeEmpty();

                var testResource = GetV5Indexer(created.Id);
                SetNewznabSettings(testResource.Fields, server.RootUrl);
                ApiV5.Post("indexer/test", testResource, HttpStatusCode.NoContent);
                ShouldContainSuccessfulTestAllResult(ApiV5.Post("indexer/testall", new object(), HttpStatusCode.OK), created.Id);

                ApiV5.Delete($"indexer/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"indexer/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"indexer/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_cover_v3_indexer_compatibility_against_mock_newznab()
        {
            using var server = NewznabTestServer.Start();
            var createdId = 0;

            try
            {
                var payload = BuildV3NewznabIndexer(server.RootUrl);
                var created = Read<V3IndexerResource>(ApiV3.Post("indexer?skipTesting=true", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);

                var byId = Read<V3IndexerResource>(ApiV3.Get($"indexer/{created.Id}"));
                SetNewznabSettings(byId.Fields, server.RootUrl);
                byId.Name = TestData.NextName("indexer-v3-renamed");
                byId.EnableInteractiveSearch = true;

                var updated = Read<V3IndexerResource>(ApiV3.Put($"indexer/{byId.Id}?skipTesting=true", byId, HttpStatusCode.Accepted));
                updated.Name.Should().Be(byId.Name);
                updated.EnableInteractiveSearch.Should().BeTrue();

                var bulkResult = Read<List<V3IndexerResource>>(PutV3IndexerBulk(
                    new V3IndexerBulkResource
                    {
                        Ids = new List<int> { created.Id },
                        EnableAutomaticSearch = true
                    },
                    HttpStatusCode.Accepted));

                bulkResult.Single(indexer => indexer.Id == created.Id).EnableAutomaticSearch.Should().BeTrue();
                var testResource = Read<V3IndexerResource>(ApiV3.Get($"indexer/{created.Id}"));
                SetNewznabSettings(testResource.Fields, server.RootUrl);
                ApiV3.Post("indexer/test", testResource, HttpStatusCode.OK);

                ApiV3.Delete($"indexer/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"indexer/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"indexer/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_search_releases_with_mock_newznab_and_return_expected_grab_errors()
        {
            using var server = NewznabTestServer.Start();
            var indexerId = 0;

            try
            {
                indexerId = Read<V5IndexerResource>(ApiV5.Post("indexer?skipTesting=true", BuildV5NewznabIndexer(server.RootUrl, enableRss: true), HttpStatusCode.Created)).Id;

                var releases = ApiV5.Get("release").ShouldHaveJsonArrayContent();

                releases.Should().NotBeEmpty();
                var firstRelease = releases.First();
                var release = firstRelease?["release"];
                release.Should().NotBeNull();
                release!["indexerId"]!.GetValue<int>().Should().Be(indexerId);
                release["guid"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
                release["title"]!.GetValue<string>().Should().Be("White.Collar.S03E05.720p.HDTV.X264-DIMENSION");
                release["downloadUrl"]!.GetValue<string>().Should().Contain("/getnzb/");
                firstRelease!["parsedInfo"].Should().NotBeNull();

                ApiV5.Post("release", new V5ReleaseGrabResource { Guid = "missing-release", IndexerId = indexerId }, HttpStatusCode.NotFound);

                var v3Releases = ApiV3.Get("release").ShouldHaveJsonArrayContent();
                var expectedTitle = release["title"]!.GetValue<string>();
                var containsExpectedV3Release = v3Releases.Any(item =>
                    item != null &&
                    item["indexerId"]?.GetValue<int>() == indexerId &&
                    item["title"]?.GetValue<string>() == expectedTitle);
                containsExpectedV3Release.Should().BeTrue();
                ApiV3.Post("release", new V3ReleaseResource { Guid = "missing-release", IndexerId = indexerId }, HttpStatusCode.NotFound);
            }
            finally
            {
                if (indexerId != 0)
                {
                    ApiV5.Delete($"indexer/{indexerId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_validate_release_push_inputs()
        {
            ShouldHaveValidationErrorFor(ApiV5.Post("release/push", new V5ReleasePushResource(), HttpStatusCode.BadRequest), "title");
            ShouldHaveValidationErrorFor(ApiV5.Post("release/push", BuildV5UnparseablePushRelease(), HttpStatusCode.BadRequest), "title");

            ShouldHaveValidationErrorFor(ApiV3.Post("release/push", new V3ReleaseResource(), HttpStatusCode.BadRequest), "title");
            ShouldHaveValidationErrorFor(ApiV3.Post("release/push", BuildV3UnparseablePushRelease(), HttpStatusCode.BadRequest), "title");
        }

        [Test]
        public void should_create_update_and_delete_v5_release_profile()
        {
            var createdId = 0;

            try
            {
                var payload = BuildV5ReleaseProfile();
                var created = Read<V5ReleaseProfileResource>(ApiV5.Post("releaseprofile", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);
                created.Required.Should().BeEquivalentTo(payload.Required);
                GetV5ReleaseProfiles().Should().Contain(profile => profile.Id == created.Id && profile.Name == payload.Name);

                var byId = GetV5ReleaseProfile(created.Id);
                byId.Name = TestData.NextName("release-profile-renamed");
                byId.Ignored = new List<string> { "CAM" };

                var updated = Read<V5ReleaseProfileResource>(ApiV5.Put($"releaseprofile/{byId.Id}", byId, HttpStatusCode.Accepted));
                updated.Name.Should().Be(byId.Name);
                updated.Ignored.Should().BeEquivalentTo("CAM");

                ApiV5.Delete($"releaseprofile/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"releaseprofile/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"releaseprofile/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_validate_release_profiles_and_cover_v3_compatibility()
        {
            ShouldHaveValidationErrorFor(
                ApiV5.Post(
                    "releaseprofile",
                    new V5ReleaseProfileResource { Name = TestData.NextName("invalid-profile") },
                    HttpStatusCode.BadRequest),
                "required");
            ShouldHaveValidationErrorFor(
                ApiV5.Post(
                    "releaseprofile",
                    new V5ReleaseProfileResource
                    {
                        Name = TestData.NextName("invalid-profile-empty-term"),
                        Required = new List<string> { string.Empty }
                    },
                    HttpStatusCode.BadRequest),
                "required");
            ApiV5.Get("releaseprofile/1000000", HttpStatusCode.NotFound);
            ApiV5.Put("releaseprofile/1000000", BuildV5ReleaseProfile(), HttpStatusCode.NotFound);

            var createdId = 0;
            try
            {
                var payload = BuildV3ReleaseProfile();
                var created = Read<V3ReleaseProfileResource>(ApiV3.Post("releaseprofile", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);
                created.Required.Should().NotBeNull();

                var byId = Read<V3ReleaseProfileResource>(ApiV3.Get($"releaseprofile/{created.Id}"));
                byId.Name = TestData.NextName("release-profile-v3-renamed");
                byId.Required = "proper,real";
                byId.Ignored = "cam,ts";

                var updated = Read<V3ReleaseProfileResource>(ApiV3.Put($"releaseprofile/{byId.Id}", byId, HttpStatusCode.Accepted));
                updated.Name.Should().Be(byId.Name);

                ApiV3.Delete($"releaseprofile/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"releaseprofile/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"releaseprofile/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        private static T Read<T>(IRestResponse response)
            where T : new()
        {
            response.ShouldHaveJsonContent();

            return Json.Deserialize<T>(response.Content);
        }

        private static JsonArray ShouldHaveValidationErrorFor(IRestResponse response, string propertyName)
        {
            var errors = response.ShouldHaveValidationErrors();

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return errors;
            }

            var propertyNames = errors
                .Select(error => error?["propertyName"]?.GetValue<string>())
                .Where(name => name != null);

            propertyNames.Should().Contain(name => string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase));

            return errors;
        }

        private static void SetFieldValue(IEnumerable<Field> fields, string name, object value)
        {
            fields.Single(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)).Value = value;
        }

        private static void ShouldContainSuccessfulTestAllResult(IRestResponse response, int id)
        {
            var result = response.ShouldHaveJsonArrayContent();
            var containsExpectedResult = result.Any(item =>
            {
                var itemId = item?["id"]?.GetValue<int>();
                var isValid = item?["isValid"]?.GetValue<bool>();

                return itemId == id && isValid == true;
            });

            containsExpectedResult.Should().BeTrue();
        }

        private static void ShouldRejectUnauthenticatedProviderRequests<TProviderResource>(VersionedApiClient api, string resource, TProviderResource body)
        {
            ExecuteUnauthenticated(api, resource, Method.GET);
            ExecuteUnauthenticated(api, $"{resource}/1", Method.GET);
            ExecuteUnauthenticated(api, $"{resource}/schema", Method.GET);
            ExecuteUnauthenticated(api, resource, Method.POST, body);
            ExecuteUnauthenticated(api, $"{resource}/1", Method.PUT, body);
            ExecuteUnauthenticated(api, $"{resource}/1", Method.DELETE);
            ExecuteUnauthenticated(api, $"{resource}/test", Method.POST, body);
            ExecuteUnauthenticated(api, $"{resource}/testall", Method.POST, new object());
            ExecuteUnauthenticated(api, $"{resource}/action/noop", Method.POST, body);
        }

        private static void ExecuteUnauthenticated(VersionedApiClient api, string resource, Method method, object body = null)
        {
            var request = api.BuildRequest(resource, method);

            if (body != null)
            {
                request.AddJsonBody(body);
            }

            api.UnauthenticatedRestClient.Execute(request).ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
        }

        private IRestResponse PutV5IndexerBulk(V5IndexerBulkResource body, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            var request = ApiV5.BuildRequest("indexer/bulk", Method.PUT);
            request.AddJsonBody(body);

            return ApiV5.Execute(request, statusCode, authenticated);
        }

        private IRestResponse PutV3IndexerBulk(V3IndexerBulkResource body, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            var request = ApiV3.BuildRequest("indexer/bulk", Method.PUT);
            request.AddJsonBody(body);

            return ApiV3.Execute(request, statusCode, authenticated);
        }

        private List<V5IndexerResource> GetV5IndexerSchema()
        {
            return Read<List<V5IndexerResource>>(ApiV5.Get("indexer/schema"));
        }

        private V5IndexerResource GetV5IndexerSchema(string implementation)
        {
            return GetV5IndexerSchema().Single(indexer => indexer.Implementation == implementation);
        }

        private V5IndexerResource GetV5Indexer(int id)
        {
            return Read<V5IndexerResource>(ApiV5.Get($"indexer/{id}"));
        }

        private List<V5IndexerResource> GetV5Indexers()
        {
            return Read<List<V5IndexerResource>>(ApiV5.Get("indexer"));
        }

        private V5IndexerResource BuildV5NewznabIndexer(string baseUrl, bool enableRss = false)
        {
            var resource = GetV5IndexerSchema("Newznab");
            resource.Name = TestData.NextName("newznab");
            resource.EnableRss = enableRss;
            resource.EnableAutomaticSearch = false;
            resource.EnableInteractiveSearch = false;
            resource.Priority = 25;
            resource.Protocol = DownloadProtocol.Usenet;
            SetNewznabSettings(resource.Fields, baseUrl);

            return resource;
        }

        private V3IndexerResource BuildV3NewznabIndexer(string baseUrl)
        {
            var resource = Read<List<V3IndexerResource>>(ApiV3.Get("indexer/schema")).Single(indexer => indexer.Implementation == "Newznab");
            resource.Name = TestData.NextName("newznab-v3");
            resource.EnableRss = true;
            resource.EnableAutomaticSearch = false;
            resource.EnableInteractiveSearch = false;
            resource.Protocol = DownloadProtocol.Usenet;
            SetNewznabSettings(resource.Fields, baseUrl);

            return resource;
        }

        private static void SetNewznabSettings(IEnumerable<Field> fields, string baseUrl)
        {
            SetFieldValue(fields, "baseUrl", baseUrl);
            SetFieldValue(fields, "apiPath", "/api");
            SetFieldValue(fields, "apiKey", "test-api-key");
            SetFieldValue(fields, "categories", "5030,5040");
            SetFieldValue(fields, "animeCategories", string.Empty);
        }

        private List<V5ReleaseProfileResource> GetV5ReleaseProfiles()
        {
            return Read<List<V5ReleaseProfileResource>>(ApiV5.Get("releaseprofile"));
        }

        private V5ReleaseProfileResource GetV5ReleaseProfile(int id)
        {
            return Read<V5ReleaseProfileResource>(ApiV5.Get($"releaseprofile/{id}"));
        }

        private V5ReleaseProfileResource BuildV5ReleaseProfile()
        {
            return new V5ReleaseProfileResource
            {
                Name = TestData.NextName("release-profile"),
                Enabled = true,
                Required = new List<string> { "PROPER" },
                Ignored = new List<string>(),
                Tags = new HashSet<int>(),
                ExcludedTags = new HashSet<int>()
            };
        }

        private V3ReleaseProfileResource BuildV3ReleaseProfile()
        {
            return new V3ReleaseProfileResource
            {
                Name = TestData.NextName("release-profile-v3"),
                Enabled = true,
                Required = "proper,real",
                Ignored = string.Empty,
                IndexerId = 0,
                Tags = new HashSet<int>(),
                ExcludedTags = new HashSet<int>()
            };
        }

        private V5ReleasePushResource BuildV5UnparseablePushRelease()
        {
            return new V5ReleasePushResource
            {
                Title = "not-a-series-release",
                DownloadUrl = "http://127.0.0.1/release.nzb",
                Protocol = DownloadProtocol.Usenet,
                PublishDate = DateTime.UtcNow,
                Size = 1024
            };
        }

        private V3ReleaseResource BuildV3UnparseablePushRelease()
        {
            return new V3ReleaseResource
            {
                Title = "not-a-series-release",
                DownloadUrl = "http://127.0.0.1/release.nzb",
                Protocol = DownloadProtocol.Usenet,
                PublishDate = DateTime.UtcNow,
                Size = 1024
            };
        }

        private sealed class NewznabTestServer : IDisposable
        {
            private const string CapabilitiesXml = """
                <?xml version="1.0" encoding="utf-8" ?>
                <caps>
                  <server appversion="" version="0.1" />
                  <limits max="60" default="25"/>
                  <registration available="yes" open="no"/>
                  <searching>
                    <search available="yes" supportedParams="q" />
                    <tv-search available="yes" supportedParams="q,tvdbid,season,ep" />
                  </searching>
                  <categories>
                    <category id="5000" name="TV">
                      <subcat id="5030" name="SD"/>
                      <subcat id="5040" name="HD"/>
                    </category>
                  </categories>
                </caps>
                """;

            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly Task _listenerTask;

            private NewznabTestServer(TcpListener listener, string rootUrl)
            {
                _listener = listener;
                RootUrl = rootUrl;
                _cancellationTokenSource = new CancellationTokenSource();
                _listenerTask = Task.Run(() => RunAsync(_cancellationTokenSource.Token));
            }

            public string RootUrl { get; }

            public static NewznabTestServer Start()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var rootUrl = $"http://127.0.0.1:{port}/";

                return new NewznabTestServer(listener, rootUrl);
            }

            public void Dispose()
            {
                _cancellationTokenSource.Cancel();
                _listener.Stop();

                try
                {
                    _listenerTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }

                _cancellationTokenSource.Dispose();
            }

            private async Task RunAsync(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (SocketException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _ = Task.Run(() => RespondAsync(client, cancellationToken), cancellationToken);
                }
            }

            private async Task RespondAsync(TcpClient client, CancellationToken cancellationToken)
            {
                using (client)
                {
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

                    var requestLine = await reader.ReadLineAsync(cancellationToken);
                    string line;

                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
                    {
                    }

                    var target = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? string.Empty;
                    var content = target.Contains("t=caps", StringComparison.OrdinalIgnoreCase)
                        ? CapabilitiesXml
                        : RssXml(RootUrl);

                    var bytes = Encoding.UTF8.GetBytes(content);
                    var headers = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/rss+xml\r\n" +
                        $"Content-Length: {bytes.Length}\r\n" +
                        "Connection: close\r\n" +
                        "\r\n");

                    await stream.WriteAsync(headers, cancellationToken);
                    await stream.WriteAsync(bytes, cancellationToken);
                }
            }

            private static string RssXml(string rootUrl)
            {
                var url = rootUrl.TrimEnd('/');

                return $"""
                    <?xml version="1.0" encoding="utf-8" ?>
                    <rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">
                      <channel>
                        <atom:link href="{url}/api?t=tvsearch&amp;cat=5030,5040&amp;apikey=test-api-key" rel="self" type="application/rss+xml" />
                        <title>Local Newznab</title>
                        <description>Local Newznab Feed</description>
                        <link>{url}/</link>
                        <language>en-gb</language>
                        <newznab:response offset="0" total="1" />
                        <item>
                          <title>White.Collar.S03E05.720p.HDTV.X264-DIMENSION</title>
                          <guid isPermaLink="true">{url}/details/24967ef4c2e26296c65d3bbfa97aa8fe</guid>
                          <link>{url}/getnzb/24967ef4c2e26296c65d3bbfa97aa8fe.nzb&amp;i=37292&amp;r=test</link>
                          <comments>{url}/details/24967ef4c2e26296c65d3bbfa97aa8fe#comments</comments>
                          <pubDate>Mon, 27 Feb 2012 11:09:39 -0500</pubDate>
                          <category>TV &gt; HD</category>
                          <description>White.Collar.S03E05.720p.HDTV.X264-DIMENSION</description>
                          <enclosure url="{url}/getnzb/24967ef4c2e26296c65d3bbfa97aa8fe.nzb&amp;i=37292&amp;r=test" length="1183105773" type="application/x-nzb" />
                          <newznab:attr name="category" value="5000" />
                          <newznab:attr name="category" value="5040" />
                          <newznab:attr name="size" value="1183105773" />
                          <newznab:attr name="guid" value="24967ef4c2e26296c65d3bbfa97aa8fe" />
                        </item>
                      </channel>
                    </rss>
                    """;
            }
        }
    }
}
