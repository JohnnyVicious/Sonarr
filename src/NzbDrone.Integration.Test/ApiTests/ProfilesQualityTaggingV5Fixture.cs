using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using Sonarr.Api.V3.CustomFormats;
using Sonarr.Api.V3.Profiles.Delay;
using V5AutoTaggingResource = Sonarr.Api.V5.AutoTagging.AutoTaggingResource;
using V5AutoTaggingSpecificationSchema = Sonarr.Api.V5.AutoTagging.AutoTaggingSpecificationSchema;
using V5QualityDefinitionResource = Sonarr.Api.V5.Qualities.QualityDefinitionResource;
using V5QualityProfileResource = Sonarr.Api.V5.Profiles.Quality.QualityProfileResource;
using JsonArray = System.Text.Json.Nodes.JsonArray;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class ProfilesQualityTaggingV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_profile_quality_tagging_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualityprofile", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "qualityprofile", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualityprofile/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualityprofile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "qualityprofile/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "qualityprofile/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualityprofile/schema", HttpStatusCode.OK);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualitydefinition", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "qualitydefinition", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualitydefinition/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "qualitydefinition/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "qualitydefinition/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "qualitydefinition/{id}", HttpStatusCode.NotFound);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "autotagging", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "autotagging", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "autotagging/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "autotagging/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "autotagging/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "autotagging/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "autotagging/schema", HttpStatusCode.OK);

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "customformat", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.POST, "customformat", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "customformat/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.PUT, "customformat/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.DELETE, "customformat/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.PUT, "customformat/bulk", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.DELETE, "customformat/bulk", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "customformat/schema", HttpStatusCode.OK);

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "delayprofile", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.POST, "delayprofile", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "delayprofile/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.PUT, "delayprofile/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.DELETE, "delayprofile/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.PUT, "delayprofile/reorder/{id}", HttpStatusCode.OK);
        }

        [Test]
        public void should_reject_unauthenticated_profile_quality_tagging_requests()
        {
            ApiV5.Get("qualityprofile", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("qualityprofile/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("qualityprofile/schema", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Post("qualityprofile", new V5QualityProfileResource(), HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("qualityprofile/1", new V5QualityProfileResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Delete("qualityprofile/1", HttpStatusCode.Unauthorized, authenticated: false);

            ApiV5.Get("qualitydefinition", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("qualitydefinition/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("qualitydefinition/1", new V5QualityDefinitionResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("qualitydefinition", new List<V5QualityDefinitionResource>(), HttpStatusCode.Unauthorized, authenticated: false);

            ApiV5.Get("autotagging", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("autotagging/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("autotagging/schema", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Post("autotagging", new V5AutoTaggingResource(), HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("autotagging/1", new V5AutoTaggingResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Delete("autotagging/1", HttpStatusCode.Unauthorized, authenticated: false);

            ApiV3.Get("customformat", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Get("customformat/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Get("customformat/schema", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Post("customformat", new CustomFormatResource(), HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Put("customformat/1", new CustomFormatResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Delete("customformat/1", HttpStatusCode.Unauthorized, authenticated: false);
            PutCustomFormatsBulk(new CustomFormatBulkResource(), HttpStatusCode.Unauthorized, authenticated: false);
            DeleteCustomFormatsBulk(new CustomFormatBulkResource(), HttpStatusCode.Unauthorized, authenticated: false);

            ApiV3.Get("delayprofile", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Get("delayprofile/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Post("delayprofile", new DelayProfileResource(), HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Put("delayprofile/1", new DelayProfileResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Delete("delayprofile/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV3.Put("delayprofile/reorder/1", new object(), HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_return_validation_errors_and_not_found_responses()
        {
            ShouldHaveValidationErrorFor(
                ApiV5.Post("qualityprofile", new V5QualityProfileResource(), HttpStatusCode.BadRequest),
                "name");
            ApiV5.Get("qualityprofile/1000000", HttpStatusCode.NotFound);
            var missingQualityProfile = GetQualityProfileSchema();
            missingQualityProfile.Id = 1000000;
            missingQualityProfile.Name = TestData.NextName("missing-quality-profile");
            ApiV5.Put("qualityprofile/1000000", missingQualityProfile, HttpStatusCode.NotFound);

            ApiV5.Get("qualitydefinition/1000000", HttpStatusCode.NotFound);
            var missingQualityDefinition = CopyQualityDefinition(GetQualityDefinitions().First());
            missingQualityDefinition.Id = 1000000;
            ApiV5.Put("qualitydefinition/1000000", missingQualityDefinition, HttpStatusCode.NotFound);

            ShouldHaveValidationErrorFor(
                ApiV5.Post("autotagging", new V5AutoTaggingResource(), HttpStatusCode.BadRequest),
                "name");
            ApiV5.Get("autotagging/1000000", HttpStatusCode.NotFound);

            ShouldHaveValidationErrorFor(
                ApiV3.Post("customformat", new CustomFormatResource { Specifications = new List<CustomFormatSpecificationSchema>() }, HttpStatusCode.BadRequest),
                "name");
            ApiV3.Get("customformat/1000000", HttpStatusCode.NotFound);

            ShouldHaveValidationErrorFor(
                ApiV3.Post("delayprofile", new DelayProfileResource { EnableUsenet = false, EnableTorrent = false, Tags = new HashSet<int>() }, HttpStatusCode.BadRequest),
                string.Empty);
            ApiV3.Get("delayprofile/1000000", HttpStatusCode.NotFound);
        }

        [Test]
        public void should_create_update_and_delete_v5_quality_profile()
        {
            var createdId = 0;

            try
            {
                var payload = GetQualityProfileSchema();
                payload.Name = TestData.NextName("quality-profile");

                var created = Read<V5QualityProfileResource>(ApiV5.Post("qualityprofile", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                GetQualityProfiles().Should().Contain(profile => profile.Id == created.Id && profile.Name == payload.Name);

                var byId = GetQualityProfile(created.Id);
                byId.Name.Should().Be(payload.Name);

                byId.Name = TestData.NextName("quality-profile-renamed");
                var updated = Read<V5QualityProfileResource>(ApiV5.Put($"qualityprofile/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                GetQualityProfile(created.Id).Name.Should().Be(byId.Name);

                ApiV5.Delete($"qualityprofile/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"qualityprofile/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"qualityprofile/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_update_v5_quality_definition_by_id_and_bulk()
        {
            var original = CopyQualityDefinition(GetQualityDefinitions().First());

            try
            {
                var singleUpdate = CopyQualityDefinition(original);
                singleUpdate.Title = $"{original.Title} API";

                var updated = Read<V5QualityDefinitionResource>(ApiV5.Put($"qualitydefinition/{singleUpdate.Id}", singleUpdate, HttpStatusCode.Accepted));

                updated.Title.Should().Be(singleUpdate.Title);
                GetQualityDefinitions().Single(definition => definition.Id == original.Id).Title.Should().Be(singleUpdate.Title);

                var bulkUpdate = GetQualityDefinitions().Select(CopyQualityDefinition).ToList();
                bulkUpdate.Single(definition => definition.Id == original.Id).Title = original.Title;

                var bulkResult = Read<List<V5QualityDefinitionResource>>(ApiV5.Put("qualitydefinition", bulkUpdate, HttpStatusCode.OK));

                bulkResult.Single(definition => definition.Id == original.Id).Title.Should().Be(original.Title);
                GetQualityDefinitions().Single(definition => definition.Id == original.Id).Title.Should().Be(original.Title);
            }
            finally
            {
                ApiV5.Put($"qualitydefinition/{original.Id}", original, HttpStatusCode.Accepted);
            }
        }

        [Test]
        public void should_create_update_and_delete_v5_auto_tagging()
        {
            var createdId = 0;

            try
            {
                var tag = TestData.Tag();
                var specification = GetAutoTaggingSchema("MonitoredSpecification");
                specification.Name = "Monitored";
                specification.Required = true;

                var payload = new V5AutoTaggingResource
                {
                    Name = TestData.NextName("auto-tag"),
                    RemoveTagsAutomatically = true,
                    Tags = new HashSet<int> { tag.Id },
                    Specifications = new List<V5AutoTaggingSpecificationSchema> { specification }
                };

                var created = Read<V5AutoTaggingResource>(ApiV5.Post("autotagging", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                created.Tags.Should().BeEquivalentTo(new[] { tag.Id });
                GetAutoTags().Should().Contain(autoTag => autoTag.Id == created.Id && autoTag.Tags.SetEquals(new[] { tag.Id }));

                var byId = GetAutoTag(created.Id);
                byId.Name = TestData.NextName("auto-tag-renamed");
                byId.RemoveTagsAutomatically = false;
                byId.Specifications.Single().Negate = true;

                var updated = Read<V5AutoTaggingResource>(ApiV5.Put($"autotagging/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                updated.RemoveTagsAutomatically.Should().BeFalse();
                updated.Specifications.Single().Negate.Should().BeTrue();
                GetAutoTag(created.Id).Name.Should().Be(byId.Name);

                ApiV5.Delete($"autotagging/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"autotagging/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"autotagging/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_create_update_bulk_update_and_delete_v3_custom_formats()
        {
            var createdId = 0;

            try
            {
                var specification = GetCustomFormatSchema("ReleaseTitleSpecification");
                specification.Name = "Release title";
                specification.Required = true;
                SetFieldValue(specification.Fields, "value", "api-test-release-title");

                var payload = new CustomFormatResource
                {
                    Name = TestData.NextName("custom-format"),
                    IncludeCustomFormatWhenRenaming = false,
                    Specifications = new List<CustomFormatSpecificationSchema> { specification }
                };

                var created = Read<CustomFormatResource>(ApiV3.Post("customformat", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                GetCustomFormats().Should().Contain(format => format.Id == created.Id && format.Name == payload.Name);

                var byId = GetCustomFormat(created.Id);
                byId.Name = TestData.NextName("custom-format-renamed");
                byId.IncludeCustomFormatWhenRenaming = true;

                var updated = Read<CustomFormatResource>(ApiV3.Put($"customformat/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                updated.IncludeCustomFormatWhenRenaming.Should().BeTrue();
                GetCustomFormat(created.Id).Name.Should().Be(byId.Name);

                var bulk = PutCustomFormatsBulk(new CustomFormatBulkResource
                {
                    Ids = new HashSet<int> { created.Id },
                    IncludeCustomFormatWhenRenaming = false
                }, HttpStatusCode.Accepted);

                bulk.Should().ContainSingle(format => format.Id == created.Id && format.IncludeCustomFormatWhenRenaming == false);
                GetCustomFormat(created.Id).IncludeCustomFormatWhenRenaming.Should().BeFalse();

                DeleteCustomFormatsBulk(new CustomFormatBulkResource { Ids = new HashSet<int> { created.Id } }, HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"customformat/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"customformat/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_create_update_reorder_and_delete_v3_delay_profiles()
        {
            var createdId = 0;

            try
            {
                var tag = TestData.Tag();
                var payload = new DelayProfileResource
                {
                    EnableUsenet = true,
                    EnableTorrent = true,
                    PreferredProtocol = DownloadProtocol.Usenet,
                    UsenetDelay = 5,
                    TorrentDelay = 10,
                    BypassIfHighestQuality = false,
                    BypassIfAboveCustomFormatScore = false,
                    MinimumCustomFormatScore = 0,
                    Tags = new HashSet<int> { tag.Id }
                };

                var created = Read<DelayProfileResource>(ApiV3.Post("delayprofile", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Tags.Should().BeEquivalentTo(new[] { tag.Id });
                GetDelayProfiles().Should().Contain(profile => profile.Id == created.Id);

                var byId = GetDelayProfile(created.Id);
                byId.UsenetDelay = 15;
                byId.TorrentDelay = 20;
                byId.BypassIfAboveCustomFormatScore = true;
                byId.MinimumCustomFormatScore = 100;

                var updated = Read<DelayProfileResource>(ApiV3.Put($"delayprofile/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.UsenetDelay.Should().Be(15);
                updated.TorrentDelay.Should().Be(20);
                updated.BypassIfAboveCustomFormatScore.Should().BeTrue();
                updated.MinimumCustomFormatScore.Should().Be(100);
                GetDelayProfile(created.Id).TorrentDelay.Should().Be(20);

                var reordered = ReorderDelayProfile(created.Id, 1);

                reordered.Should().Contain(profile => profile.Id == created.Id);

                ApiV3.Delete($"delayprofile/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"delayprofile/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"delayprofile/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        private V5QualityProfileResource GetQualityProfileSchema()
        {
            return Read<V5QualityProfileResource>(ApiV5.Get("qualityprofile/schema"));
        }

        private List<V5QualityProfileResource> GetQualityProfiles()
        {
            return Read<List<V5QualityProfileResource>>(ApiV5.Get("qualityprofile"));
        }

        private V5QualityProfileResource GetQualityProfile(int id)
        {
            return Read<V5QualityProfileResource>(ApiV5.Get($"qualityprofile/{id}"));
        }

        private List<V5QualityDefinitionResource> GetQualityDefinitions()
        {
            return Read<List<V5QualityDefinitionResource>>(ApiV5.Get("qualitydefinition"));
        }

        private static V5QualityDefinitionResource CopyQualityDefinition(V5QualityDefinitionResource resource)
        {
            return new V5QualityDefinitionResource
            {
                Id = resource.Id,
                Quality = resource.Quality,
                Title = resource.Title,
                Weight = resource.Weight
            };
        }

        private List<V5AutoTaggingResource> GetAutoTags()
        {
            return Read<List<V5AutoTaggingResource>>(ApiV5.Get("autotagging"));
        }

        private V5AutoTaggingResource GetAutoTag(int id)
        {
            return Read<V5AutoTaggingResource>(ApiV5.Get($"autotagging/{id}"));
        }

        private V5AutoTaggingSpecificationSchema GetAutoTaggingSchema(string implementation)
        {
            return Read<List<V5AutoTaggingSpecificationSchema>>(ApiV5.Get("autotagging/schema"))
                .Single(schema => schema.Implementation == implementation);
        }

        private List<CustomFormatResource> GetCustomFormats()
        {
            return Read<List<CustomFormatResource>>(ApiV3.Get("customformat"));
        }

        private CustomFormatResource GetCustomFormat(int id)
        {
            return Read<CustomFormatResource>(ApiV3.Get($"customformat/{id}"));
        }

        private CustomFormatSpecificationSchema GetCustomFormatSchema(string implementation)
        {
            return Read<List<CustomFormatSpecificationSchema>>(ApiV3.Get("customformat/schema"))
                .Single(schema => schema.Implementation == implementation);
        }

        private List<DelayProfileResource> GetDelayProfiles()
        {
            return Read<List<DelayProfileResource>>(ApiV3.Get("delayprofile"));
        }

        private DelayProfileResource GetDelayProfile(int id)
        {
            return Read<DelayProfileResource>(ApiV3.Get($"delayprofile/{id}"));
        }

        private List<DelayProfileResource> ReorderDelayProfile(int id, int afterId)
        {
            return Read<List<DelayProfileResource>>(ApiV3.Put($"delayprofile/reorder/{id}?after={afterId}", new object(), HttpStatusCode.OK));
        }

        private List<CustomFormatResource> PutCustomFormatsBulk(CustomFormatBulkResource resource, HttpStatusCode statusCode, bool authenticated = true)
        {
            var request = ApiV3.BuildRequest("customformat/bulk", Method.PUT);
            request.AddJsonBody(resource);
            var response = ApiV3.Execute(request, statusCode, authenticated);

            if (statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Accepted)
            {
                return new List<CustomFormatResource>();
            }

            return Read<List<CustomFormatResource>>(response);
        }

        private void DeleteCustomFormatsBulk(CustomFormatBulkResource resource, HttpStatusCode statusCode, bool authenticated = true)
        {
            var request = ApiV3.BuildRequest("customformat/bulk", Method.DELETE);
            request.AddJsonBody(resource);

            ApiV3.Execute(request, statusCode, authenticated);
        }

        private static void SetFieldValue(IEnumerable<Sonarr.Http.ClientSchema.Field> fields, string fieldName, object value)
        {
            fields.Single(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase)).Value = value;
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
    }
}
