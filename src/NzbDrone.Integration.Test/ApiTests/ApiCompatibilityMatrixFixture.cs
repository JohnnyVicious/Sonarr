using System.Globalization;
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
    public class ApiCompatibilityMatrixFixture : IntegrationTest
    {
        private static readonly string[] SharedJsonGetRoutes =
        {
            "system/status",
            "health",
            "command",
            "diskspace",
            "update",
            "qualityprofile",
            "qualityprofile/{id}",
            "rootfolder",
            "rootfolder/{id}",
            "tag",
            "tag/{id}",
            "tag/detail",
            "tag/detail/{id}",
            "series",
            "series/{id}",
            "episode",
            "episode/{id}",
            "queue",
            "queue/details",
            "queue/status",
            "calendar",
            "calendar/{id}"
        };

        [Test]
        public void should_declare_shared_compatibility_routes_in_openapi()
        {
            foreach (var api in new[] { ApiV3, ApiV5 })
            {
                foreach (var route in SharedJsonGetRoutes)
                {
                    api.OpenApi.GetResponseSchema(Method.GET, route, HttpStatusCode.OK).Should().NotBeNull();
                }

                api.OpenApi.ShouldDeclareResponse(Method.GET, "feed/calendar/sonarr.ics", HttpStatusCode.OK);
            }

            // The current v3 feed contract declares the route/status only; v5 also declares text/calendar content.
            ApiV5.OpenApi
                .GetResponseSchema(Method.GET, "feed/calendar/sonarr.ics", HttpStatusCode.OK, "text/calendar")["type"]!
                .GetValue<string>()
                .Should()
                .Be("string");
        }

        [Test]
        public void should_return_compatible_common_resources_from_v3_and_v5()
        {
            EnsureNoSeries(266189, "The Blacklist");

            var rootFolder = TestData.RootFolder();
            var tag = TestData.Tag();
            var series = TestData.Series(266189, "The Blacklist", true, tag);
            var episode = TestData.Episodes(series).First(item => item.SeasonNumber > 0 && item.AirDateUtc.HasValue);
            var calendarStart = episode.AirDateUtc!.Value.AddDays(-1).ToString("s", CultureInfo.InvariantCulture) + "Z";
            var calendarEnd = episode.AirDateUtc.Value.AddDays(1).ToString("s", CultureInfo.InvariantCulture) + "Z";

            var v3Status = ReadObject(ApiV3.Get("system/status"));
            var v5Status = ReadObject(ApiV5.Get("system/status"));
            v5Status["appName"]!.GetValue<string>().Should().Be(v3Status["appName"]!.GetValue<string>());
            v5Status["version"]!.GetValue<string>().Should().Be(v3Status["version"]!.GetValue<string>());

            ReadArray(ApiV3.Get("health")).Should().NotBeNull();
            ReadArray(ApiV5.Get("health")).Should().NotBeNull();
            ReadArray(ApiV3.Get("command")).Should().NotBeNull();
            ReadArray(ApiV5.Get("command")).Should().NotBeNull();
            ReadArray(ApiV3.Get("diskspace")).Should().NotBeNull();
            ReadArray(ApiV5.Get("diskspace")).Should().NotBeNull();
            ReadArray(ApiV3.Get("update")).Should().NotBeNull();
            ReadArray(ApiV5.Get("update")).Should().NotBeNull();

            AssertSharedArrayResource("qualityprofile", "id", 1, "name");
            AssertSharedObjectResource("qualityprofile/1", "id", 1, "name");
            AssertSharedArrayResource("rootfolder", "id", rootFolder.Id, "path");
            AssertSharedObjectResource($"rootfolder/{rootFolder.Id}", "id", rootFolder.Id, "path");
            AssertSharedArrayResource("tag", "id", tag.Id, "label");
            AssertSharedObjectResource($"tag/{tag.Id}", "id", tag.Id, "label");
            AssertSharedArrayResource("tag/detail", "id", tag.Id, "label");
            AssertSharedObjectResource($"tag/detail/{tag.Id}", "id", tag.Id, "label");
            AssertSharedArrayResource($"series?tvdbId={series.TvdbId}", "id", series.Id, "title");
            AssertSharedObjectResource($"series/{series.Id}", "id", series.Id, "title");
            AssertSharedArrayResource($"episode?seriesId={series.Id}", "id", episode.Id, "title");
            AssertSharedObjectResource($"episode/{episode.Id}", "id", episode.Id, "title");
            AssertSharedObjectIntResource($"calendar/{episode.Id}", "id", episode.Id, "seriesId");

            var v3Queue = ReadObject(ApiV3.Get("queue?page=1&pageSize=10&sortKey=added&sortDirection=descending"));
            var v5Queue = ReadObject(ApiV5.Get("queue?page=1&pageSize=10&sortKey=added&sortDirection=descending"));
            v3Queue["records"].Should().NotBeNull();
            v5Queue["records"].Should().NotBeNull();

            ReadArray(ApiV3.Get("queue/details")).Should().NotBeNull();
            ReadArray(ApiV5.Get("queue/details")).Should().NotBeNull();
            ReadObject(ApiV3.Get("queue/status"))["totalCount"].Should().NotBeNull();
            ReadObject(ApiV5.Get("queue/status"))["totalCount"].Should().NotBeNull();

            var v3Calendar = ReadArray(ApiV3.Get($"calendar?start={calendarStart}&end={calendarEnd}"));
            var v5Calendar = ReadArray(ApiV5.Get($"calendar?start={calendarStart}&end={calendarEnd}"));
            FindByInt(v3Calendar, "seriesId", series.Id)["title"]!.GetValue<string>().Should().Be(episode.Title);
            FindByInt(v5Calendar, "seriesId", series.Id)["title"]!.GetValue<string>().Should().Be(episode.Title);

            AssertCalendarFeed(ApiV3);
            AssertCalendarFeed(ApiV5);
        }

        private void AssertSharedArrayResource(string resource, string idProperty, int id, string scalarProperty)
        {
            var v3Resource = FindByInt(ReadArray(ApiV3.Get(resource)), idProperty, id);
            var v5Resource = FindByInt(ReadArray(ApiV5.Get(resource)), idProperty, id);

            v5Resource[scalarProperty]!.GetValue<string>().Should().Be(v3Resource[scalarProperty]!.GetValue<string>());
        }

        private void AssertSharedObjectResource(string resource, string idProperty, int id, string scalarProperty)
        {
            var v3Resource = ReadObject(ApiV3.Get(resource));
            var v5Resource = ReadObject(ApiV5.Get(resource));

            v3Resource[idProperty]!.GetValue<int>().Should().Be(id);
            v5Resource[idProperty]!.GetValue<int>().Should().Be(id);
            v5Resource[scalarProperty]!.GetValue<string>().Should().Be(v3Resource[scalarProperty]!.GetValue<string>());
        }

        private void AssertSharedObjectIntResource(string resource, string idProperty, int id, string scalarProperty)
        {
            var v3Resource = ReadObject(ApiV3.Get(resource));
            var v5Resource = ReadObject(ApiV5.Get(resource));

            v3Resource[idProperty]!.GetValue<int>().Should().Be(id);
            v5Resource[idProperty]!.GetValue<int>().Should().Be(id);
            v5Resource[scalarProperty]!.GetValue<int>().Should().Be(v3Resource[scalarProperty]!.GetValue<int>());
        }

        private static void AssertCalendarFeed(VersionedApiClient api)
        {
            var feed = api.Get("feed/calendar/sonarr.ics?pastDays=0&futureDays=0");

            feed.StatusCode.Should().Be(HttpStatusCode.OK);
            feed.ContentType.Should().StartWith("text/calendar");
            feed.Content.Should().Contain("VCALENDAR");
        }

        private static JsonObject FindByInt(JsonArray items, string propertyName, int value)
        {
            var matches = items
                .OfType<JsonObject>()
                .Where(item => item[propertyName] != null && item[propertyName]!.GetValue<int>() == value)
                .ToList();

            matches.Should().ContainSingle($"{propertyName} {value} should be present in the response");

            return matches.Single();
        }

        private static JsonObject ReadObject(IRestResponse response)
        {
            return response.ShouldHaveJsonObjectContent();
        }

        private static JsonArray ReadArray(IRestResponse response)
        {
            return response.ShouldHaveJsonArrayContent();
        }
    }
}
