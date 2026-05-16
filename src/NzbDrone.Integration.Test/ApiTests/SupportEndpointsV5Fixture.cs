using System;
using System.IO;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class SupportEndpointsV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_support_routes_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rootfolder", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "rootfolder", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rootfolder/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rootfolder/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "rootfolder/{id}", HttpStatusCode.NoContent);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "remotepathmapping", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "remotepathmapping", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "remotepathmapping/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "remotepathmapping/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "remotepathmapping/{id}", HttpStatusCode.NoContent);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "tag", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "tag", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "tag/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "tag/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "tag/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "tag/detail", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "tag/detail/{id}", HttpStatusCode.OK);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "customfilter", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "customfilter", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "customfilter/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "customfilter/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "customfilter/{id}", HttpStatusCode.NoContent);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "filesystem", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "filesystem/mediafiles", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "filesystem/type", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "parse", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rename", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rename/bulk", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "rename/bulk", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "manualimport", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "manualimport", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "manualimport", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "calendar", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "feed/calendar/sonarr.ics", HttpStatusCode.OK);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "localization", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "localization/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "localization/language", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "language", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "language/{id}", HttpStatusCode.OK);

            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "importlistexclusion", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "importlistexclusion", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "importlistexclusion/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "importlistexclusion/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "importlistexclusion/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "importlistexclusion/bulk", HttpStatusCode.NoContent);

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "languageprofile", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "languageprofile/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "languageprofile/schema", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.POST, "languageprofile");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "languageprofile/{id}");
            ApiV3.OpenApi.ShouldDeclareOperation(Method.DELETE, "languageprofile/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "mediacover/{seriesId}/{filename}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "importlistexclusion/paged", HttpStatusCode.OK);
        }

        [Test]
        public void should_reject_unauthenticated_support_requests()
        {
            var tempPath = Uri.EscapeDataString(GetTempDirectory("Auth"));

            ExecuteUnauthenticated(ApiV5, "rootfolder", Method.GET);
            ExecuteUnauthenticated(ApiV5, "rootfolder/1", Method.GET);
            ExecuteUnauthenticated(ApiV5, "rootfolder", Method.POST, new { path = GetTempDirectory("AuthRoot") });
            ExecuteUnauthenticated(ApiV5, "rootfolder/1", Method.DELETE);
            ExecuteUnauthenticated(ApiV5, "remotepathmapping", Method.GET);
            ExecuteUnauthenticated(ApiV5, "remotepathmapping", Method.POST, new { host = "host", remotePath = "/remote/", localPath = GetTempDirectory("AuthMapping") });
            ExecuteUnauthenticated(ApiV5, "remotepathmapping/1", Method.PUT, new { id = 1, host = "host", remotePath = "/remote/", localPath = GetTempDirectory("AuthMappingPut") });
            ExecuteUnauthenticated(ApiV5, "remotepathmapping/1", Method.DELETE);
            ExecuteUnauthenticated(ApiV5, "tag", Method.GET);
            ExecuteUnauthenticated(ApiV5, "tag", Method.POST, new { label = "unauth-tag" });
            ExecuteUnauthenticated(ApiV5, "tag/detail", Method.GET);
            ExecuteUnauthenticated(ApiV5, "tag/1", Method.DELETE);
            ExecuteUnauthenticated(ApiV5, "customfilter", Method.GET);
            ExecuteUnauthenticated(ApiV5, "customfilter", Method.POST, new { type = "series", label = "unauth-filter", filters = Array.Empty<object>() });
            ExecuteUnauthenticated(ApiV5, "customfilter/1", Method.DELETE);
            ExecuteUnauthenticated(ApiV5, $"filesystem?path={tempPath}", Method.GET);
            ExecuteUnauthenticated(ApiV5, $"filesystem/type?path={tempPath}", Method.GET);
            ExecuteUnauthenticated(ApiV5, $"filesystem/mediafiles?path={tempPath}", Method.GET);
            ExecuteUnauthenticated(ApiV5, "parse?title=The.Blacklist.S03E01.720p.HDTV.x264-GROUP", Method.GET);
            ExecuteUnauthenticated(ApiV5, "rename?seriesId=1", Method.GET);
            ExecuteUnauthenticated(ApiV5, "rename/bulk?seriesIds=1", Method.GET);
            ExecuteUnauthenticated(ApiV5, $"manualimport?folder={tempPath}", Method.GET);
            ExecuteUnauthenticated(ApiV5, "manualimport", Method.POST, Array.Empty<object>());
            ExecuteUnauthenticated(ApiV5, "calendar", Method.GET);
            ExecuteUnauthenticated(ApiV5, "localization", Method.GET);
            ExecuteUnauthenticated(ApiV5, "localization/1", Method.GET);
            ExecuteUnauthenticated(ApiV5, "localization/language", Method.GET);
            ExecuteUnauthenticated(ApiV5, "language", Method.GET);
            ExecuteUnauthenticated(ApiV5, "language/1", Method.GET);
            ExecuteUnauthenticated(ApiV5, "importlistexclusion", Method.GET);
            ExecuteUnauthenticated(ApiV5, "importlistexclusion", Method.POST, new { tvdbId = 1, title = "Unauth" });
            ExecuteUnauthenticated(ApiV5, "importlistexclusion/1", Method.DELETE);
            ExecuteUnauthenticated(ApiV5, "importlistexclusion/bulk", Method.DELETE, new { ids = new[] { 1 } });
            ExecuteUnauthenticated(ApiV5, "feed/calendar/sonarr.ics", Method.GET);

            ExecuteUnauthenticated(ApiV3, "languageprofile", Method.GET);
            ExecuteUnauthenticated(ApiV3, "languageprofile/1", Method.GET);
            ExecuteUnauthenticated(ApiV3, "languageprofile/schema", Method.GET);
            ExecuteUnauthenticated(ApiV3, "mediacover/1/poster.jpg", Method.GET);
            ExecuteUnauthenticated(ApiV3, "importlistexclusion/paged", Method.GET);
        }

        [Test]
        public void should_manage_rootfolder_remote_path_mapping_tags_and_custom_filters()
        {
            var rootFolderId = 0;
            var remotePathMappingId = 0;
            var tagId = 0;
            var customFilterId = 0;

            try
            {
                var rootPath = GetTempDirectory("RootFolder");
                var rootFolder = ReadObject(ApiV5.Post("rootfolder", new { path = rootPath }, HttpStatusCode.Created));
                rootFolderId = rootFolder["id"]!.GetValue<int>();
                rootFolder["path"]!.GetValue<string>().Should().Be(rootPath);

                ReadArray(ApiV5.Get("rootfolder")).Any(item => item?["id"]?.GetValue<int>() == rootFolderId).Should().BeTrue();
                ReadObject(ApiV5.Get($"rootfolder/{rootFolderId}"))["path"]!.GetValue<string>().Should().Be(rootPath);
                ApiV5.Get("rootfolder/1000000", HttpStatusCode.NotFound);

                var mappingLocalPath = GetTempDirectory("RemotePathMapping");
                var mapping = ReadObject(ApiV5.Post(
                    "remotepathmapping",
                    new
                    {
                        host = "support-host",
                        remotePath = "/downloads/",
                        localPath = mappingLocalPath
                    },
                    HttpStatusCode.Created));
                remotePathMappingId = mapping["id"]!.GetValue<int>();
                mapping["host"]!.GetValue<string>().Should().Be("support-host");

                var updatedMapping = ReadObject(ApiV5.Put(
                    $"remotepathmapping/{remotePathMappingId}",
                    new
                    {
                        id = remotePathMappingId,
                        host = "support-host",
                        remotePath = "/downloads-updated/",
                        localPath = mappingLocalPath
                    },
                    HttpStatusCode.OK));
                updatedMapping["remotePath"]!.GetValue<string>().Should().Be("/downloads-updated/");
                ReadArray(ApiV5.Get("remotepathmapping")).Any(item => item?["id"]?.GetValue<int>() == remotePathMappingId).Should().BeTrue();
                ApiV5.Get("remotepathmapping/1000000", HttpStatusCode.NotFound);

                var tag = ReadObject(ApiV5.Post("tag", new { label = TestData.NextName("tag") }, HttpStatusCode.Created));
                tagId = tag["id"]!.GetValue<int>();
                tag["label"]!.GetValue<string>().Should().StartWith("api-tag-");

                var updatedTag = ReadObject(ApiV5.Put($"tag/{tagId}", new { id = tagId, label = TestData.NextName("tag-updated") }, HttpStatusCode.Accepted));
                updatedTag["label"]!.GetValue<string>().Should().StartWith("api-tag-updated-");
                ReadObject(ApiV5.Get($"tag/detail/{tagId}"))["id"]!.GetValue<int>().Should().Be(tagId);
                ReadArray(ApiV5.Get("tag/detail")).Any(item => item?["id"]?.GetValue<int>() == tagId).Should().BeTrue();

                var customFilter = ReadObject(ApiV5.Post(
                    "customfilter",
                    new
                    {
                        type = "series",
                        label = TestData.NextName("filter"),
                        filters = Array.Empty<object>()
                    },
                    HttpStatusCode.Created));
                customFilterId = customFilter["id"]!.GetValue<int>();
                customFilter["type"]!.GetValue<string>().Should().Be("series");

                var updatedFilter = ReadObject(ApiV5.Put(
                    $"customfilter/{customFilterId}",
                    new
                    {
                        id = customFilterId,
                        type = "series",
                        label = TestData.NextName("filter-updated"),
                        filters = Array.Empty<object>()
                    },
                    HttpStatusCode.Accepted));
                updatedFilter["label"]!.GetValue<string>().Should().StartWith("api-filter-updated-");
                ReadArray(ApiV5.Get("customfilter")).Any(item => item?["id"]?.GetValue<int>() == customFilterId).Should().BeTrue();
                ApiV5.Get("customfilter/1000000", HttpStatusCode.NotFound);
            }
            finally
            {
                DeleteIfCreated("customfilter", customFilterId);
                DeleteIfCreated("tag", tagId);
                DeleteIfCreated("remotepathmapping", remotePathMappingId);
                DeleteIfCreated("rootfolder", rootFolderId);
            }
        }

        [Test]
        public void should_cover_filesystem_parse_rename_manualimport_calendar_localization_and_language()
        {
            var folder = GetTempDirectory("SupportFiles");
            var childFolder = Path.Combine(folder, "child");
            var videoPath = Path.Combine(folder, "Some.Show.S01E01.mkv");
            Directory.CreateDirectory(childFolder);
            File.WriteAllText(videoPath, "video");

            var contentsRequest = ApiV5.BuildRequest("filesystem", Method.GET);
            contentsRequest.AddQueryParameter("path", folder);
            contentsRequest.AddQueryParameter("includeFiles", "true");
            var contents = ReadObject(ApiV5.Execute(contentsRequest));
            ReadArrayFrom(contents, "directories").Should().NotBeEmpty();
            ReadArrayFrom(contents, "files").Should().NotBeNull();

            var mediaFilesRequest = ApiV5.BuildRequest("filesystem/mediafiles", Method.GET);
            mediaFilesRequest.AddQueryParameter("path", folder);
            ReadArray(ApiV5.Execute(mediaFilesRequest)).Any(item => item?["name"]?.GetValue<string>() == Path.GetFileName(videoPath)).Should().BeTrue();

            var typeRequest = ApiV5.BuildRequest("filesystem/type", Method.GET);
            typeRequest.AddQueryParameter("path", videoPath);
            ReadObject(ApiV5.Execute(typeRequest))["type"]!.GetValue<string>().Should().Be("file");

            var missingTypeRequest = ApiV5.BuildRequest("filesystem/type", Method.GET);
            missingTypeRequest.AddQueryParameter("path", Path.Combine(folder, "missing"));
            ReadObject(ApiV5.Execute(missingTypeRequest))["type"]!.GetValue<string>().Should().Be("folder");

            var parse = ReadObject(ApiV5.Get("parse?title=The.Blacklist.S03E01.720p.HDTV.x264-GROUP"));
            parse["parsedEpisodeInfo"].Should().NotBeNull();
            parse["title"]!.GetValue<string>().Should().Contain("The.Blacklist");

            var series = TestData.Series(266189, "The Blacklist", true);
            ReadArray(ApiV5.Get($"rename?seriesId={series.Id}")).Should().NotBeNull();
            ReadArray(ApiV5.Get($"rename/bulk?seriesIds={series.Id}")).Should().NotBeNull();
            ApiV5.Get("rename/bulk", HttpStatusCode.BadRequest).ShouldHaveJsonObjectContent()["message"]!.GetValue<string>().Should().Contain("seriesIds must be provided");

            var manualImportRequest = ApiV5.BuildRequest("manualimport", Method.GET);
            manualImportRequest.AddQueryParameter("folder", GetTempDirectory("ManualImport"));
            ReadArray(ApiV5.Execute(manualImportRequest)).Should().BeEmpty();
            ApiV5.Post("manualimport", Array.Empty<object>(), HttpStatusCode.BadRequest).ShouldHaveJsonObjectContent()["message"]!.GetValue<string>().Should().Contain("items must be provided");

            var calendar = ReadArray(ApiV5.Get("calendar?start=2015-10-01T00:00:00Z&end=2015-10-03T00:00:00Z&includeUnmonitored=true&includeSubresources=series"));
            calendar.Any(item => item?["seriesId"]?.GetValue<int>() == series.Id).Should().BeTrue();

            var feed = ApiV5.Get("feed/calendar/sonarr.ics?pastDays=0&futureDays=0");
            feed.ContentType.Should().StartWith("text/calendar");
            feed.Content.Should().Contain("VCALENDAR");

            var localization = ReadObject(ApiV5.Get("localization"));
            ReadObjectFrom(localization, "strings").Should().NotBeEmpty();
            ReadObject(ApiV5.Get("localization/1"))["strings"].Should().NotBeNull();
            ReadObject(ApiV5.Get("localization/language"))["identifier"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

            var languages = ReadArray(ApiV5.Get("language"));
            languages.Any(item => item?["name"]?.GetValue<string>() == "English").Should().BeTrue();
            ReadObject(ApiV5.Get("language/1"))["name"]!.GetValue<string>().Should().Be("English");
        }

        [Test]
        public void should_manage_import_list_exclusions_and_v3_legacy_support_endpoints()
        {
            var firstId = 0;
            var secondId = 0;
            var tvdbId = 900000000 + (int)(DateTime.UtcNow.Ticks % 1000000);

            try
            {
                var first = ReadObject(ApiV5.Post("importlistexclusion", new { tvdbId, title = TestData.NextName("exclusion") }, HttpStatusCode.Created));
                firstId = first["id"]!.GetValue<int>();
                first["tvdbId"]!.GetValue<int>().Should().Be(tvdbId);

                var updated = ReadObject(ApiV5.Put(
                    $"importlistexclusion/{firstId}",
                    new
                    {
                        id = firstId,
                        tvdbId,
                        title = TestData.NextName("exclusion-updated")
                    },
                    HttpStatusCode.Accepted));
                updated["title"]!.GetValue<string>().Should().StartWith("api-exclusion-updated-");
                ReadObject(ApiV5.Get($"importlistexclusion/{firstId}"))["id"]!.GetValue<int>().Should().Be(firstId);

                var page = ReadObject(ApiV5.Get("importlistexclusion?page=1&pageSize=10&sortKey=id&sortDirection=descending"));
                ReadArrayFrom(page, "records").Any(item => item?["id"]?.GetValue<int>() == firstId).Should().BeTrue();

                var v3Page = ReadObject(ApiV3.Get("importlistexclusion/paged?page=1&pageSize=10&sortKey=id&sortDirection=descending"));
                ReadArrayFrom(v3Page, "records").Any(item => item?["id"]?.GetValue<int>() == firstId).Should().BeTrue();

                secondId = ReadObject(ApiV5.Post("importlistexclusion", new { tvdbId = tvdbId + 1, title = TestData.NextName("exclusion") }, HttpStatusCode.Created))["id"]!.GetValue<int>();
                ExecuteWithJsonBody(ApiV5, "importlistexclusion/bulk", Method.DELETE, new { ids = new[] { firstId, secondId } }, HttpStatusCode.NoContent);
                firstId = 0;
                secondId = 0;
                ApiV5.Get($"importlistexclusion/{updated["id"]!.GetValue<int>()}", HttpStatusCode.NotFound);

                var languageProfiles = ReadArray(ApiV3.Get("languageprofile"));
                languageProfiles.Should().ContainSingle();
                languageProfiles[0]!["name"]!.GetValue<string>().Should().Be("Deprecated");
                ReadObject(ApiV3.Get("languageprofile/1"))["name"]!.GetValue<string>().Should().Be("Deprecated");
                ReadObject(ApiV3.Get("languageprofile/schema"))["name"]!.GetValue<string>().Should().Be("Deprecated");

                var profilePayload = new
                {
                    id = 1,
                    name = "Deprecated",
                    upgradeAllowed = true,
                    cutoff = new { id = 1, name = "English" },
                    languages = new[] { new { language = new { id = 1, name = "English" }, allowed = true } }
                };
                ApiV3.Post("languageprofile", profilePayload, HttpStatusCode.Accepted).ShouldHaveJsonObjectContent()["name"]!.GetValue<string>().Should().Be("Deprecated");
                ApiV3.Put("languageprofile/1", profilePayload, HttpStatusCode.Accepted).ShouldHaveJsonObjectContent()["name"]!.GetValue<string>().Should().Be("Deprecated");
                ApiV3.Delete("languageprofile/1", HttpStatusCode.OK);

                var appData = ReadObject(ApiV5.Get("system/status"))["appData"]!.GetValue<string>();
                var coverFolder = Path.Combine(appData, "MediaCover", "1");
                var coverPath = Path.Combine(coverFolder, "poster.jpg");

                try
                {
                    Directory.CreateDirectory(coverFolder);
                    File.WriteAllBytes(coverPath, new byte[] { 0xff, 0xd8, 0xff, 0xd9 });

                    var cover = ApiV3.Get("mediacover/1/poster.jpg");
                    cover.ContentType.Should().StartWith("image/jpeg");
                    cover.RawBytes.Should().NotBeEmpty();
                    ApiV3.Get("mediacover/1/missing.jpg", HttpStatusCode.NotFound);
                }
                finally
                {
                    if (Directory.Exists(coverFolder))
                    {
                        Directory.Delete(coverFolder, true);
                    }
                }
            }
            finally
            {
                DeleteIfCreated("importlistexclusion", firstId);
                DeleteIfCreated("importlistexclusion", secondId);
            }
        }

        private static JsonObject ReadObject(IRestResponse response)
        {
            return response.ShouldHaveJsonObjectContent();
        }

        private static JsonArray ReadArray(IRestResponse response)
        {
            return response.ShouldHaveJsonArrayContent();
        }

        private static JsonObject ReadObjectFrom(JsonObject parent, string propertyName)
        {
            var child = parent[propertyName] as JsonObject;
            child.Should().NotBeNull();

            return child;
        }

        private static JsonArray ReadArrayFrom(JsonObject parent, string propertyName)
        {
            var child = parent[propertyName] as JsonArray;
            child.Should().NotBeNull();

            return child;
        }

        private void DeleteIfCreated(string resource, int id)
        {
            if (id > 0)
            {
                ApiV5.Delete($"{resource}/{id}", HttpStatusCode.NoContent);
            }
        }

        private void ExecuteUnauthenticated(VersionedApiClient api, string resource, Method method, object body = null)
        {
            var request = api.BuildRequest(resource, method);

            if (body != null)
            {
                request.AddJsonBody(body);
            }

            var client = api.UnauthenticatedRestClient;

            if (request.Resource.StartsWith("feed/", StringComparison.Ordinal))
            {
                request.Resource = request.Resource["feed/".Length..];
                client = api.UnauthenticatedFeedRestClient;
            }

            client.Execute(request).ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
        }

        private static IRestResponse ExecuteWithJsonBody(VersionedApiClient api, string resource, Method method, object body, HttpStatusCode statusCode)
        {
            var request = api.BuildRequest(resource, method);
            request.AddJsonBody(body);

            return api.Execute(request, statusCode);
        }
    }
}
