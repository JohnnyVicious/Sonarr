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
using NzbDrone.Core.Tv;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using Sonarr.Api.V3;
using Sonarr.Http.ClientSchema;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using V3DownloadClientBulkResource = Sonarr.Api.V3.DownloadClient.DownloadClientBulkResource;
using V3DownloadClientResource = Sonarr.Api.V3.DownloadClient.DownloadClientResource;
using V3ImportListBulkResource = Sonarr.Api.V3.ImportLists.ImportListBulkResource;
using V3ImportListResource = Sonarr.Api.V3.ImportLists.ImportListResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class DownloadClientImportListV3Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v3_only_downloadclient_importlist_operations_in_openapi()
        {
            foreach (var resource in new[] { "downloadclient", "importlist" })
            {
                ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, resource, HttpStatusCode.OK);
                ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, resource);
                ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, $"{resource}/{{id}}", HttpStatusCode.OK);
                ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, $"{resource}/{{id}}");
                ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, $"{resource}/{{id}}");
                ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, $"{resource}/bulk");
                ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, $"{resource}/bulk");
                ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, $"{resource}/schema", HttpStatusCode.OK);
                ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, $"{resource}/test");
                ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, $"{resource}/testall");
                ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, $"{resource}/action/{{name}}");
            }
        }

        [Test]
        public void should_reject_unauthenticated_v3_downloadclient_importlist_requests()
        {
            ShouldRejectUnauthenticatedProviderRequests(ApiV3, "downloadclient", new V3DownloadClientResource());
            ExecuteUnauthenticated(ApiV3, "downloadclient/bulk", Method.PUT, new V3DownloadClientBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV3, "downloadclient/bulk", Method.DELETE, new V3DownloadClientBulkResource { Ids = new List<int> { 1 } });

            ShouldRejectUnauthenticatedProviderRequests(ApiV3, "importlist", new V3ImportListResource());
            ExecuteUnauthenticated(ApiV3, "importlist/bulk", Method.PUT, new V3ImportListBulkResource { Ids = new List<int> { 1 } });
            ExecuteUnauthenticated(ApiV3, "importlist/bulk", Method.DELETE, new V3ImportListBulkResource { Ids = new List<int> { 1 } });
        }

        [Test]
        public void should_return_downloadclient_and_importlist_provider_schemas()
        {
            var downloadClients = GetDownloadClientSchemas();
            var usenetBlackhole = downloadClients.Single(client => client.Implementation == "UsenetBlackhole");

            usenetBlackhole.ConfigContract.Should().Be("UsenetBlackholeSettings");
            usenetBlackhole.Protocol.Should().Be(DownloadProtocol.Usenet);
            usenetBlackhole.Fields.Should().Contain(field => field.Name == "watchFolder");
            usenetBlackhole.Fields.Should().Contain(field => field.Name == "nzbFolder");

            var importLists = GetImportListSchemas();
            var customImport = importLists.Single(list => list.Implementation == "CustomImport");

            customImport.ConfigContract.Should().Be("CustomSettings");
            customImport.Fields.Should().Contain(field => field.Name == "baseUrl");
            customImport.MinRefreshInterval.Should().Be(TimeSpan.FromHours(6));
        }

        [Test]
        public void should_create_update_and_delete_downloadclient()
        {
            var createdId = 0;

            try
            {
                var payload = BuildUsenetBlackholeDownloadClient();
                var created = Read<V3DownloadClientResource>(ApiV3.Post("downloadclient", payload, HttpStatusCode.Created));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);
                created.Enable.Should().BeTrue();
                GetDownloadClients().Should().Contain(client => client.Id == created.Id && client.Name == payload.Name);

                var byId = GetDownloadClient(created.Id);
                byId.Name = TestData.NextName("usenet-blackhole-renamed");
                byId.Priority = 15;
                byId.RemoveCompletedDownloads = false;
                byId.RemoveFailedDownloads = false;
                SetFieldValue(byId.Fields, "nzbFolder", GetTempDirectory("Download", byId.Name, "Nzb2"));

                var updated = Read<V3DownloadClientResource>(ApiV3.Put($"downloadclient/{created.Id}", byId, HttpStatusCode.Accepted));
                updated.Name.Should().Be(byId.Name);
                updated.Priority.Should().Be(15);
                updated.RemoveCompletedDownloads.Should().BeFalse();
                updated.RemoveFailedDownloads.Should().BeFalse();

                ApiV3.Delete($"downloadclient/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"downloadclient/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"downloadclient/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_bulk_update_downloadclient()
        {
            var createdId = 0;

            try
            {
                var created = Read<V3DownloadClientResource>(ApiV3.Post("downloadclient", BuildUsenetBlackholeDownloadClient(), HttpStatusCode.Created));
                createdId = created.Id;

                var bulkResult = Read<List<V3DownloadClientResource>>(ApiV3.Put(
                    "downloadclient/bulk",
                    new V3DownloadClientBulkResource
                    {
                        Ids = new List<int> { created.Id },
                        Tags = new List<int> { TestData.Tag().Id },
                        ApplyTags = ApplyTags.Replace,
                        Enable = false,
                        Priority = 30,
                        RemoveCompletedDownloads = true,
                        RemoveFailedDownloads = true
                    },
                    HttpStatusCode.Accepted));

                var bulkUpdated = bulkResult.Single(client => client.Id == created.Id);
                bulkUpdated.Enable.Should().BeFalse();
                bulkUpdated.Priority.Should().Be(30);
                bulkUpdated.RemoveCompletedDownloads.Should().BeTrue();
                bulkUpdated.RemoveFailedDownloads.Should().BeTrue();
                bulkUpdated.Tags.Should().NotBeEmpty();
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"downloadclient/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_test_and_request_downloadclient_action()
        {
            var createdId = 0;

            try
            {
                var created = Read<V3DownloadClientResource>(ApiV3.Post("downloadclient", BuildUsenetBlackholeDownloadClient(), HttpStatusCode.Created));
                createdId = created.Id;

                var testResource = GetDownloadClient(created.Id);
                testResource.Enable = true;
                ApiV3.Post("downloadclient/test", testResource, HttpStatusCode.OK);

                Read<V3DownloadClientResource>(ApiV3.Put($"downloadclient/{created.Id}", testResource, HttpStatusCode.Accepted));
                ShouldContainSuccessfulTestAllResult(ApiV3.Post("downloadclient/testall", new object(), HttpStatusCode.OK), created.Id);

                ApiV3.Post("downloadclient/action/noop", testResource, HttpStatusCode.OK).Content.Should().Be("null");
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"downloadclient/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_bulk_delete_downloadclients()
        {
            var firstId = 0;
            var secondId = 0;

            try
            {
                firstId = Read<V3DownloadClientResource>(ApiV3.Post("downloadclient", BuildUsenetBlackholeDownloadClient(), HttpStatusCode.Created)).Id;
                secondId = Read<V3DownloadClientResource>(ApiV3.Post("downloadclient", BuildUsenetBlackholeDownloadClient(), HttpStatusCode.Created)).Id;

                DeleteDownloadClientBulk(new V3DownloadClientBulkResource { Ids = new List<int> { firstId, secondId } });

                ApiV3.Get($"downloadclient/{firstId}", HttpStatusCode.NotFound);
                ApiV3.Get($"downloadclient/{secondId}", HttpStatusCode.NotFound);
                firstId = 0;
                secondId = 0;
            }
            finally
            {
                if (firstId != 0)
                {
                    ApiV3.Delete($"downloadclient/{firstId}", HttpStatusCode.OK);
                }

                if (secondId != 0)
                {
                    ApiV3.Delete($"downloadclient/{secondId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_validate_downloadclient_inputs_and_missing_resources()
        {
            var invalid = BuildUsenetBlackholeDownloadClient();
            invalid.Name = string.Empty;

            ShouldHaveValidationErrorFor(ApiV3.Post("downloadclient", invalid, HttpStatusCode.BadRequest), "name");
            ApiV3.Get("downloadclient/1000000", HttpStatusCode.NotFound);
            ApiV3.Put("downloadclient/1000000", BuildUsenetBlackholeDownloadClient(), HttpStatusCode.NotFound);
            ApiV3.Put("downloadclient/bulk", new V3DownloadClientBulkResource(), HttpStatusCode.BadRequest);
        }

        [Test]
        public void should_create_update_and_delete_importlist_against_mock_endpoint()
        {
            using var server = JsonImportListServer.Start("[]");
            var createdId = 0;

            try
            {
                var rootFolder = TestData.RootFolder("custom-import-root");
                var payload = BuildCustomImportList(server.RootUrl, rootFolder.Path, enableAutomaticAdd: true);
                var created = Read<V3ImportListResource>(PostImportList(payload));
                createdId = created.Id;

                created.Id.Should().BeGreaterThan(0);
                created.Name.Should().Be(payload.Name);
                created.EnableAutomaticAdd.Should().BeTrue();
                created.RootFolderPath.Should().Be(rootFolder.Path);
                created.QualityProfileId.Should().Be(TestData.QualityProfile().Id);
                GetImportLists().Should().Contain(list => list.Id == created.Id && list.Name == payload.Name);

                var byId = GetImportList(created.Id);
                byId.Name = TestData.NextName("custom-import-renamed");
                byId.SearchForMissingEpisodes = true;
                byId.SeasonFolder = false;
                SetFieldValue(byId.Fields, "baseUrl", server.RootUrl);

                var updated = Read<V3ImportListResource>(PutImportList(created.Id, byId));
                updated.Name.Should().Be(byId.Name);
                updated.SearchForMissingEpisodes.Should().BeTrue();
                updated.SeasonFolder.Should().BeFalse();

                ApiV3.Delete($"importlist/{created.Id}", HttpStatusCode.OK);
                createdId = 0;

                ApiV3.Get($"importlist/{created.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"importlist/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_bulk_update_importlist_against_mock_endpoint()
        {
            using var server = JsonImportListServer.Start("[]");
            var createdId = 0;

            try
            {
                var rootFolder = TestData.RootFolder("custom-import-root");
                var created = Read<V3ImportListResource>(PostImportList(BuildCustomImportList(server.RootUrl, rootFolder.Path)));
                createdId = created.Id;
                var bulkRootFolder = TestData.RootFolder("custom-import-bulk-root");

                var bulkResult = Read<List<V3ImportListResource>>(ApiV3.Put(
                    "importlist/bulk",
                    new V3ImportListBulkResource
                    {
                        Ids = new List<int> { created.Id },
                        Tags = new List<int> { TestData.Tag().Id },
                        ApplyTags = ApplyTags.Replace,
                        EnableAutomaticAdd = false,
                        RootFolderPath = bulkRootFolder.Path,
                        QualityProfileId = TestData.QualityProfile().Id
                    },
                    HttpStatusCode.Accepted));

                var bulkUpdated = bulkResult.Single(list => list.Id == created.Id);
                bulkUpdated.EnableAutomaticAdd.Should().BeFalse();
                bulkUpdated.RootFolderPath.Should().Be(bulkRootFolder.Path);
                bulkUpdated.Tags.Should().NotBeEmpty();
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"importlist/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_test_and_request_importlist_action_against_mock_endpoint()
        {
            using var server = JsonImportListServer.Start("[]");
            var createdId = 0;

            try
            {
                var rootFolder = TestData.RootFolder("custom-import-root");
                var created = Read<V3ImportListResource>(PostImportList(BuildCustomImportList(server.RootUrl, rootFolder.Path)));
                createdId = created.Id;
                var testResource = GetImportList(created.Id);

                PostImportListTest(testResource);

                testResource.EnableAutomaticAdd = true;
                Read<V3ImportListResource>(PutImportList(created.Id, testResource));
                ShouldContainSuccessfulTestAllResult(ApiV3.Post("importlist/testall", new object(), HttpStatusCode.OK), created.Id);

                PostImportListAction("noop", testResource).ShouldHaveJsonObjectContent();
            }
            finally
            {
                if (createdId != 0)
                {
                    ApiV3.Delete($"importlist/{createdId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_bulk_delete_importlists()
        {
            using var server = JsonImportListServer.Start("[]");
            var firstId = 0;
            var secondId = 0;

            try
            {
                var rootFolder = TestData.RootFolder("bulk-delete-import-root");
                firstId = Read<V3ImportListResource>(PostImportList(BuildCustomImportList(server.RootUrl, rootFolder.Path))).Id;
                secondId = Read<V3ImportListResource>(PostImportList(BuildCustomImportList(server.RootUrl, rootFolder.Path))).Id;

                DeleteImportListBulk(new V3ImportListBulkResource { Ids = new List<int> { firstId, secondId } });

                ApiV3.Get($"importlist/{firstId}", HttpStatusCode.NotFound);
                ApiV3.Get($"importlist/{secondId}", HttpStatusCode.NotFound);
                firstId = 0;
                secondId = 0;
            }
            finally
            {
                if (firstId != 0)
                {
                    ApiV3.Delete($"importlist/{firstId}", HttpStatusCode.OK);
                }

                if (secondId != 0)
                {
                    ApiV3.Delete($"importlist/{secondId}", HttpStatusCode.OK);
                }
            }
        }

        [Test]
        public void should_validate_importlist_inputs_and_missing_resources()
        {
            using var server = JsonImportListServer.Start("[]");
            var rootFolder = TestData.RootFolder("invalid-import-root");
            var invalid = BuildCustomImportList(server.RootUrl, rootFolder.Path);
            invalid.Name = string.Empty;

            ShouldHaveValidationErrorFor(PostImportList(invalid, HttpStatusCode.BadRequest), "name");
            ApiV3.Get("importlist/1000000", HttpStatusCode.NotFound);
            PutImportList(1000000, BuildCustomImportList(server.RootUrl, rootFolder.Path), HttpStatusCode.NotFound);
            ApiV3.Put("importlist/bulk", new V3ImportListBulkResource(), HttpStatusCode.BadRequest);
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

        private V3DownloadClientResource BuildUsenetBlackholeDownloadClient()
        {
            var resource = GetDownloadClientSchemas().Single(client => client.Implementation == "UsenetBlackhole");
            resource.Name = TestData.NextName("usenet-blackhole");
            resource.Enable = true;
            resource.Protocol = DownloadProtocol.Usenet;
            resource.Priority = 25;
            resource.RemoveCompletedDownloads = true;
            resource.RemoveFailedDownloads = true;
            SetFieldValue(resource.Fields, "watchFolder", GetTempDirectory("Download", resource.Name, "Watch"));
            SetFieldValue(resource.Fields, "nzbFolder", GetTempDirectory("Download", resource.Name, "Nzb"));

            return resource;
        }

        private V3ImportListResource BuildCustomImportList(string baseUrl, string rootFolderPath, bool enableAutomaticAdd = false)
        {
            var resource = GetImportListSchemas().Single(list => list.Implementation == "CustomImport");
            resource.Name = TestData.NextName("custom-import");
            resource.EnableAutomaticAdd = enableAutomaticAdd;
            resource.SearchForMissingEpisodes = false;
            resource.ShouldMonitor = MonitorTypes.All;
            resource.MonitorNewItems = NewItemMonitorTypes.All;
            resource.RootFolderPath = rootFolderPath;
            resource.QualityProfileId = TestData.QualityProfile().Id;
            resource.SeriesType = SeriesTypes.Standard;
            resource.SeasonFolder = true;
            SetFieldValue(resource.Fields, "baseUrl", baseUrl);

            return resource;
        }

        private V3DownloadClientResource GetDownloadClient(int id)
        {
            return Read<V3DownloadClientResource>(ApiV3.Get($"downloadclient/{id}"));
        }

        private List<V3DownloadClientResource> GetDownloadClients()
        {
            return Read<List<V3DownloadClientResource>>(ApiV3.Get("downloadclient"));
        }

        private List<V3DownloadClientResource> GetDownloadClientSchemas()
        {
            return Read<List<V3DownloadClientResource>>(ApiV3.Get("downloadclient/schema"));
        }

        private V3ImportListResource GetImportList(int id)
        {
            return Read<V3ImportListResource>(ApiV3.Get($"importlist/{id}"));
        }

        private List<V3ImportListResource> GetImportLists()
        {
            return Read<List<V3ImportListResource>>(ApiV3.Get("importlist"));
        }

        private List<V3ImportListResource> GetImportListSchemas()
        {
            return Read<List<V3ImportListResource>>(ApiV3.Get("importlist/schema"));
        }

        private IRestResponse PostImportList(V3ImportListResource body, HttpStatusCode statusCode = HttpStatusCode.Created)
        {
            return ApiV3.Post("importlist", ToWritableImportListBody(body), statusCode);
        }

        private IRestResponse PutImportList(int id, V3ImportListResource body, HttpStatusCode statusCode = HttpStatusCode.Accepted)
        {
            return ApiV3.Put($"importlist/{id}", ToWritableImportListBody(body), statusCode);
        }

        private IRestResponse PostImportListTest(V3ImportListResource body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return ApiV3.Post("importlist/test", ToWritableImportListBody(body), statusCode);
        }

        private IRestResponse PostImportListAction(string action, V3ImportListResource body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return ApiV3.Post($"importlist/action/{action}", ToWritableImportListBody(body), statusCode);
        }

        private static object ToWritableImportListBody(V3ImportListResource resource)
        {
            return new
            {
                resource.Id,
                resource.Name,
                resource.Fields,
                resource.ImplementationName,
                resource.Implementation,
                resource.ConfigContract,
                resource.InfoLink,
                resource.Message,
                resource.Tags,
                resource.Presets,
                resource.EnableAutomaticAdd,
                resource.SearchForMissingEpisodes,
                resource.ShouldMonitor,
                resource.MonitorNewItems,
                resource.RootFolderPath,
                resource.QualityProfileId,
                resource.SeriesType,
                resource.SeasonFolder,
                resource.ListType,
                resource.ListOrder
            };
        }

        private IRestResponse DeleteDownloadClientBulk(V3DownloadClientBulkResource body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = ApiV3.BuildRequest("downloadclient/bulk", Method.DELETE);
            request.AddJsonBody(body);

            return ApiV3.Execute(request, statusCode);
        }

        private IRestResponse DeleteImportListBulk(V3ImportListBulkResource body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = ApiV3.BuildRequest("importlist/bulk", Method.DELETE);
            request.AddJsonBody(body);

            return ApiV3.Execute(request, statusCode);
        }

        private sealed class JsonImportListServer : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly TcpListener _listener;
            private readonly Task _acceptLoop;
            private readonly byte[] _response;

            private JsonImportListServer(string content)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();

                var endpoint = (IPEndPoint)_listener.LocalEndpoint;
                RootUrl = $"http://127.0.0.1:{endpoint.Port}/list";
                var body = Encoding.UTF8.GetBytes(content);
                var header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                _response = header.Concat(body).ToArray();
                _acceptLoop = Task.Run(AcceptLoopAsync);
            }

            public string RootUrl { get; }

            public static JsonImportListServer Start(string content)
            {
                return new JsonImportListServer(content);
            }

            public void Dispose()
            {
                _cancellationTokenSource.Cancel();
                _listener.Stop();

                try
                {
                    _acceptLoop.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                }

                _cancellationTokenSource.Dispose();
            }

            private async Task AcceptLoopAsync()
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                try
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        var buffer = new byte[4096];
                        var request = new MemoryStream();

                        while (!_cancellationTokenSource.IsCancellationRequested)
                        {
                            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancellationTokenSource.Token);
                            if (read == 0)
                            {
                                break;
                            }

                            await request.WriteAsync(buffer.AsMemory(0, read), _cancellationTokenSource.Token);
                            if (Encoding.ASCII.GetString(request.GetBuffer(), 0, (int)request.Length).Contains("\r\n\r\n", StringComparison.Ordinal))
                            {
                                break;
                            }
                        }

                        await stream.WriteAsync(_response, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
