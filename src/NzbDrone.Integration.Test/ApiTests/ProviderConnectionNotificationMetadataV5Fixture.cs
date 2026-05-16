using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using Sonarr.Http.ClientSchema;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using V3NotificationResource = Sonarr.Api.V3.Notifications.NotificationResource;
using V5ConnectionResource = Sonarr.Api.V5.Connections.ConnectionResource;
using V5MetadataResource = Sonarr.Api.V5.Metadata.MetadataResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class ProviderConnectionNotificationMetadataV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_provider_operations_in_openapi()
        {
            foreach (var resource in new[] { "connection", "metadata" })
            {
                ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, resource, HttpStatusCode.OK);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, resource, HttpStatusCode.Created);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, resource, HttpStatusCode.NotFound);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, $"{resource}/{{id}}", HttpStatusCode.OK);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, $"{resource}/{{id}}", HttpStatusCode.NotFound);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, $"{resource}/{{id}}", HttpStatusCode.Accepted);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, $"{resource}/{{id}}", HttpStatusCode.NotFound);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, $"{resource}/{{id}}", HttpStatusCode.NoContent);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, $"{resource}/schema", HttpStatusCode.OK);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, $"{resource}/test", HttpStatusCode.NoContent);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, $"{resource}/testall", HttpStatusCode.OK);
                ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, $"{resource}/testall", HttpStatusCode.BadRequest);
                ApiV5.OpenApi.ShouldDeclareOperation(Method.POST, $"{resource}/action/{{name}}");
            }

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "notification", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "notification");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "notification/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "notification/{id}");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, "notification/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "notification/schema", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "notification/test");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "notification/testall");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "notification/action/{name}");
        }

        [Test]
        public void should_reject_unauthenticated_provider_requests()
        {
            ShouldRejectUnauthenticatedProviderRequests(ApiV5, "connection", new V5ConnectionResource());
            ShouldRejectUnauthenticatedProviderRequests(ApiV5, "metadata", new V5MetadataResource());
            ShouldRejectUnauthenticatedProviderRequests(ApiV3, "notification", new V3NotificationResource());
        }

        [Test]
        public void should_return_provider_schemas_with_stable_shapes()
        {
            var connections = GetV5ConnectionSchema();
            var notifications = GetV3NotificationSchema();
            var metadata = GetV5MetadataSchema();

            var connectionScript = connections.Single(provider => provider.Implementation == "CustomScript");
            connectionScript.ConfigContract.Should().Be("CustomScriptSettings");
            connectionScript.Fields.Should().Contain(field => field.Name == "path");
            connectionScript.SupportsOnGrab.Should().BeTrue();

            var notificationScript = notifications.Single(provider => provider.Implementation == "CustomScript");
            notificationScript.ConfigContract.Should().Be("CustomScriptSettings");
            notificationScript.Fields.Should().Contain(field => field.Name == "path");
            notificationScript.SupportsOnGrab.Should().BeTrue();

            connections.Select(provider => provider.Implementation).Should().Contain(notifications.Select(provider => provider.Implementation));

            var kodiMetadata = metadata.Single(provider => provider.Implementation == "XbmcMetadata");
            kodiMetadata.ConfigContract.Should().Be("XbmcMetadataSettings");
            kodiMetadata.Fields.Should().NotBeEmpty();
        }

        [Test]
        public void should_create_update_test_and_delete_v5_connection()
        {
            var createdId = 0;

            try
            {
                var payload = BuildV5CustomScriptConnection(CreateSuccessfulScript());

                var created = Read<V5ConnectionResource>(ApiV5.Post("connection?skipTesting=true", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                created.OnGrab.Should().BeTrue();
                GetV5Connections().Should().Contain(connection => connection.Id == created.Id && connection.Name == payload.Name);

                var byId = GetV5Connection(created.Id);
                byId.Name = TestData.NextName("connection-renamed");
                byId.OnGrab = false;
                byId.OnDownload = true;

                var updated = Read<V5ConnectionResource>(ApiV5.Put($"connection/{byId.Id}?skipTesting=true", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                updated.OnDownload.Should().BeTrue();
                GetV5Connection(created.Id).Name.Should().Be(byId.Name);

                ApiV5.Post("connection/test", byId, HttpStatusCode.NoContent);
                ApiV5.Delete($"connection/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"connection/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"connection/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_create_update_test_and_delete_v3_notification()
        {
            var createdId = 0;

            try
            {
                var payload = BuildV3CustomScriptNotification(CreateSuccessfulScript());

                var created = Read<V3NotificationResource>(ApiV3.Post("notification", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                created.OnGrab.Should().BeTrue();
                GetV3Notifications().Should().Contain(notification => notification.Id == created.Id && notification.Name == payload.Name);

                var byId = GetV3Notification(created.Id);
                byId.Name = TestData.NextName("notification-renamed");
                byId.OnGrab = false;
                byId.OnDownload = true;

                var updated = Read<V3NotificationResource>(ApiV3.Put($"notification/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                updated.OnDownload.Should().BeTrue();
                GetV3Notification(created.Id).Name.Should().Be(byId.Name);

                ApiV3.Post("notification/test", byId, HttpStatusCode.OK);
                ApiV3.Delete($"notification/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"notification/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"notification/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_create_update_test_and_delete_v5_metadata()
        {
            var createdId = 0;

            try
            {
                var payload = BuildV5Metadata();

                var created = Read<V5MetadataResource>(ApiV5.Post("metadata", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().NotBe(0);
                created.Name.Should().Be(payload.Name);
                created.Enable.Should().BeTrue();
                GetV5Metadata().Should().Contain(metadata => metadata.Id == created.Id && metadata.Name == payload.Name);

                var byId = GetV5Metadata(created.Id);
                byId.Name = TestData.NextName("metadata-renamed");
                byId.Enable = false;

                var updated = Read<V5MetadataResource>(ApiV5.Put($"metadata/{byId.Id}", byId, HttpStatusCode.Accepted));

                updated.Name.Should().Be(byId.Name);
                updated.Enable.Should().BeFalse();
                GetV5Metadata(created.Id).Name.Should().Be(byId.Name);

                ApiV5.Post("metadata/test", byId, HttpStatusCode.NoContent);
                ApiV5.Delete($"metadata/{created.Id}", HttpStatusCode.NoContent);
                createdId = 0;

                ApiV5.Get($"metadata/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV5.Delete($"metadata/{createdId}", HttpStatusCode.NoContent);
                }
            }
        }

        [Test]
        public void should_return_provider_validation_and_not_found_responses()
        {
            var scriptPath = CreateSuccessfulScript();

            ShouldHaveValidationErrorFor(ApiV5.Post("connection", new V5ConnectionResource(), HttpStatusCode.BadRequest), "name");
            ApiV5.Get("connection/1000000", HttpStatusCode.NotFound);
            ApiV5.Put("connection/1000000", BuildV5CustomScriptConnection(scriptPath), HttpStatusCode.NotFound);
            ShouldHaveValidationErrorFor(ApiV5.Post("connection/test", new V5ConnectionResource(), HttpStatusCode.BadRequest), string.Empty);

            ShouldHaveValidationErrorFor(ApiV5.Post("metadata", new V5MetadataResource(), HttpStatusCode.BadRequest), "name");
            ApiV5.Get("metadata/1000000", HttpStatusCode.NotFound);
            ApiV5.Put("metadata/1000000", BuildV5Metadata(), HttpStatusCode.NotFound);
            ShouldHaveValidationErrorFor(ApiV5.Post("metadata/test", new V5MetadataResource(), HttpStatusCode.BadRequest), string.Empty);

            ShouldHaveValidationErrorFor(ApiV3.Post("notification", new V3NotificationResource(), HttpStatusCode.BadRequest), "name");
            ApiV3.Get("notification/1000000", HttpStatusCode.NotFound);
            ApiV3.Put("notification/1000000", BuildV3CustomScriptNotification(scriptPath), HttpStatusCode.NotFound);
            ShouldHaveValidationErrorFor(ApiV3.Post("notification/test", new V3NotificationResource(), HttpStatusCode.BadRequest), string.Empty);
        }

        [Test]
        public void should_run_provider_testall_and_actions_without_external_services()
        {
            var connectionId = 0;
            var notificationId = 0;
            var metadataId = 0;

            try
            {
                var scriptPath = CreateSuccessfulScript();
                connectionId = Read<V5ConnectionResource>(ApiV5.Post("connection?skipTesting=true", BuildV5CustomScriptConnection(scriptPath), HttpStatusCode.Created)).Id;
                notificationId = Read<V3NotificationResource>(ApiV3.Post("notification", BuildV3CustomScriptNotification(scriptPath), HttpStatusCode.Created)).Id;
                metadataId = Read<V5MetadataResource>(ApiV5.Post("metadata", BuildV5Metadata(), HttpStatusCode.Created)).Id;

                ShouldContainSuccessfulTestAllResult(ApiV5.Post("connection/testall", new object(), HttpStatusCode.OK), connectionId);
                ShouldContainSuccessfulTestAllResult(ApiV3.Post("notification/testall", new object(), HttpStatusCode.OK), notificationId);
                ShouldContainSuccessfulTestAllResult(ApiV5.Post("metadata/testall", new object(), HttpStatusCode.OK), metadataId);

                ApiV5.Post("connection/action/getDevices", GetV5ConnectionSchema("PushBullet"), HttpStatusCode.OK)
                    .ShouldHaveJsonObjectContent()
                    .ContainsKey("devices")
                    .Should()
                    .BeTrue();

                ApiV3.Post("notification/action/getDevices", GetV3NotificationSchema("PushBullet"), HttpStatusCode.OK)
                    .ShouldHaveJsonObjectContent()
                    .ContainsKey("devices")
                    .Should()
                    .BeTrue();
            }
            finally
            {
                if (metadataId != 0)
                {
                    ApiV5.Delete($"metadata/{metadataId}", HttpStatusCode.NoContent);
                }

                if (notificationId != 0)
                {
                    ApiV3.Delete($"notification/{notificationId}", HttpStatusCode.OK);
                }

                if (connectionId != 0)
                {
                    ApiV5.Delete($"connection/{connectionId}", HttpStatusCode.NoContent);
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

        private V5ConnectionResource BuildV5CustomScriptConnection(string scriptPath)
        {
            var resource = GetV5ConnectionSchema("CustomScript");
            resource.Name = TestData.NextName("connection-custom-script");
            resource.OnGrab = true;
            SetFieldValue(resource.Fields, "path", scriptPath);

            return resource;
        }

        private V3NotificationResource BuildV3CustomScriptNotification(string scriptPath)
        {
            var resource = GetV3NotificationSchema("CustomScript");
            resource.Name = TestData.NextName("notification-custom-script");
            resource.OnGrab = true;
            SetFieldValue(resource.Fields, "path", scriptPath);

            return resource;
        }

        private V5MetadataResource BuildV5Metadata()
        {
            var resource = GetV5MetadataSchema("XbmcMetadata");
            resource.Name = TestData.NextName("metadata-kodi");
            resource.Enable = true;
            SetFieldValue(resource.Fields, "seriesMetadata", true);

            return resource;
        }

        private string CreateSuccessfulScript()
        {
            Directory.CreateDirectory(TempDirectory);

            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(TempDirectory, $"sonarr-provider-test-{Guid.NewGuid():N}.cmd");
                File.WriteAllText(scriptPath, "@echo off\r\nexit /b 0\r\n");

                return scriptPath;
            }

            var unixScriptPath = Path.Combine(TempDirectory, $"sonarr-provider-test-{Guid.NewGuid():N}.sh");
            File.WriteAllText(unixScriptPath, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(unixScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            return unixScriptPath;
        }

        private List<V5ConnectionResource> GetV5ConnectionSchema()
        {
            return Read<List<V5ConnectionResource>>(ApiV5.Get("connection/schema"));
        }

        private V5ConnectionResource GetV5ConnectionSchema(string implementation)
        {
            return GetV5ConnectionSchema().Single(provider => provider.Implementation == implementation);
        }

        private List<V5ConnectionResource> GetV5Connections()
        {
            return Read<List<V5ConnectionResource>>(ApiV5.Get("connection"));
        }

        private V5ConnectionResource GetV5Connection(int id)
        {
            return Read<V5ConnectionResource>(ApiV5.Get($"connection/{id}"));
        }

        private List<V3NotificationResource> GetV3NotificationSchema()
        {
            return Read<List<V3NotificationResource>>(ApiV3.Get("notification/schema"));
        }

        private V3NotificationResource GetV3NotificationSchema(string implementation)
        {
            return GetV3NotificationSchema().Single(provider => provider.Implementation == implementation);
        }

        private List<V3NotificationResource> GetV3Notifications()
        {
            return Read<List<V3NotificationResource>>(ApiV3.Get("notification"));
        }

        private V3NotificationResource GetV3Notification(int id)
        {
            return Read<V3NotificationResource>(ApiV3.Get($"notification/{id}"));
        }

        private List<V5MetadataResource> GetV5MetadataSchema()
        {
            return Read<List<V5MetadataResource>>(ApiV5.Get("metadata/schema"));
        }

        private V5MetadataResource GetV5MetadataSchema(string implementation)
        {
            return GetV5MetadataSchema().Single(provider => provider.Implementation == implementation);
        }

        private List<V5MetadataResource> GetV5Metadata()
        {
            return Read<List<V5MetadataResource>>(ApiV5.Get("metadata"));
        }

        private V5MetadataResource GetV5Metadata(int id)
        {
            return Read<V5MetadataResource>(ApiV5.Get($"metadata/{id}"));
        }
    }
}
