using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using FluentAssertions;
using Npgsql;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using RestSharp;
using Sonarr.Http;
using V3EpisodeResource = Sonarr.Api.V3.Episodes.EpisodeResource;
using V3SeriesResource = Sonarr.Api.V3.Series.SeriesResource;
using V5BlocklistBulkResource = Sonarr.Api.V5.Blocklist.BlocklistBulkResource;
using V5BlocklistResource = Sonarr.Api.V5.Blocklist.BlocklistResource;
using V5EpisodeResource = Sonarr.Api.V5.Episodes.EpisodeResource;
using V5HistoryResource = Sonarr.Api.V5.History.HistoryResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class HistoryWantedBlocklistV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v5_history_wanted_and_blocklist_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "history", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "history/since", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "history/series", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "history/season", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "history/episode", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "history/failed/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/missing", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/missing/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/missing/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/cutoff", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/cutoff/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "wanted/cutoff/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "blocklist", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "blocklist/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "blocklist/bulk", HttpStatusCode.NoContent);
        }

        [Test]
        public void should_reject_unauthenticated_v5_history_wanted_and_blocklist_requests()
        {
            ApiV5.Get("history", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("history/since?date=2000-01-01T00:00:00Z", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("history/series?seriesId=1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("history/season?seriesId=1&seasonNumber=1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("history/episode?episodeId=1", HttpStatusCode.Unauthorized, authenticated: false);
            PostHistoryFailed(1, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("wanted/missing", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("wanted/missing/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("wanted/cutoff", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("wanted/cutoff/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("blocklist", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Delete("blocklist/1", HttpStatusCode.Unauthorized, authenticated: false);
            DeleteBlocklistBulk(new V5BlocklistBulkResource { Ids = new List<int> { 1 } }, HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_page_read_and_filter_v5_wanted_missing_and_cutoff()
        {
            var missingSeries = TestData.Series(monitored: true);
            var missing = GetWantedMissing(includeSeries: true);
            var missingRecord = missing.Records.First(record => record.SeriesId == missingSeries.Id);

            missing.TotalRecords.Should().BeGreaterOrEqualTo(1);
            missingRecord.Series.Should().NotBeNull();
            missingRecord.Series.Id.Should().Be(missingSeries.Id);
            GetWantedMissingById(missingRecord.Id).Id.Should().Be(missingRecord.Id);
            ApiV5.Get("wanted/missing/1000000", HttpStatusCode.NotFound);

            var cutoffSeries = TestData.Series(79349, "Dexter", monitored: true);
            TestData.QualityProfileCutoff(Quality.HDTV720p, true);
            TestData.EpisodeFile(cutoffSeries, 1, 1, Quality.SDTV);

            var cutoff = GetWantedCutoff(includeSeries: true, includeEpisodeFile: true);
            var cutoffRecord = cutoff.Records.First(record => record.SeriesId == cutoffSeries.Id);

            cutoff.TotalRecords.Should().BeGreaterOrEqualTo(1);
            cutoffRecord.Series.Should().NotBeNull();
            cutoffRecord.EpisodeFile.Should().NotBeNull();
            GetWantedCutoffById(cutoffRecord.Id).Id.Should().Be(cutoffRecord.Id);
            ApiV5.Get("wanted/cutoff/1000000", HttpStatusCode.NotFound);
        }

        [Test]
        public void should_page_filter_and_read_v5_history()
        {
            var series = TestData.Series();
            var episode = TestData.Episode(series, 1, 1);
            var episodeFile = TestData.EpisodeFile(series, episode.SeasonNumber, episode.EpisodeNumber, Quality.SDTV);
            var beforeDelete = DateTime.UtcNow.AddMinutes(-1);

            DeleteEpisodeFile(episodeFile.Id);

            var history = WaitForHistoryRecord(record =>
                record.SeriesId == series.Id &&
                record.EpisodeId == episode.Id &&
                record.EventType == EpisodeHistoryEventType.EpisodeFileDeleted);

            history.Series.Should().NotBeNull();
            history.Episode.Should().NotBeNull();
            history.Quality.Quality.Id.Should().Be(Quality.SDTV.Id);

            GetHistoryPage(eventTypes: new[] { (int)EpisodeHistoryEventType.EpisodeFileDeleted })
                .Records.Should().Contain(record => record.Id == history.Id);
            GetHistoryPage(seriesIds: new[] { series.Id })
                .Records.Should().Contain(record => record.Id == history.Id);
            GetHistoryPage(episodeId: episode.Id)
                .Records.Should().Contain(record => record.Id == history.Id);
            GetHistoryPage(quality: new[] { Quality.SDTV.Id })
                .Records.Should().Contain(record => record.Id == history.Id);
            GetHistoryPage(languages: new[] { history.Languages.First().Id })
                .Records.Should().Contain(record => record.Id == history.Id);

            GetHistorySince(beforeDelete, EpisodeHistoryEventType.EpisodeFileDeleted, includeSubresources: true)
                .Should().Contain(record => record.Id == history.Id && record.Series != null && record.Episode != null);
            GetSeriesHistory(series.Id, EpisodeHistoryEventType.EpisodeFileDeleted, includeSubresources: true)
                .Should().Contain(record => record.Id == history.Id && record.Series != null && record.Episode != null);
            GetSeasonHistory(series.Id, episode.SeasonNumber, EpisodeHistoryEventType.EpisodeFileDeleted, includeSubresources: true)
                .Should().Contain(record => record.Id == history.Id && record.Series != null && record.Episode != null);
            GetEpisodeHistory(episode.Id, EpisodeHistoryEventType.EpisodeFileDeleted, includeSubresources: true)
                .Should().Contain(record => record.Id == history.Id && record.Series != null && record.Episode != null);

            var downloadId = TestData.NextName("download-id");
            SeedHistory(series, episode, EpisodeHistoryEventType.Grabbed, TestData.NextName("download-filter"), downloadId);

            GetHistoryPage(downloadId: downloadId)
                .Records.Should().Contain(record => record.DownloadId == downloadId);
        }

        [Test]
        public void should_mark_v5_history_failed_and_delete_v5_blocklist_items()
        {
            var series = TestData.Series();
            var episode = TestData.Episode(series, 1, 1);
            var sourceTitle = TestData.NextName("failed-history");
            var historyId = SeedHistory(series, episode, EpisodeHistoryEventType.Grabbed, sourceTitle);

            PostHistoryFailed(historyId);

            WaitForHistoryRecord(record =>
                record.SeriesId == series.Id &&
                record.EpisodeId == episode.Id &&
                record.EventType == EpisodeHistoryEventType.DownloadFailed &&
                record.SourceTitle == sourceTitle);

            var blocklist = WaitForBlocklistRecord(record => record.SeriesId == series.Id && record.SourceTitle == sourceTitle);

            GetBlocklistPage(seriesIds: new[] { series.Id })
                .Records.Should().Contain(record => record.Id == blocklist.Id);
            GetBlocklistPage(protocols: new[] { DownloadProtocol.Usenet })
                .Records.Should().Contain(record => record.Id == blocklist.Id);

            DeleteBlocklist(blocklist.Id);
            WaitForBlocklistItemToDisappear(blocklist.Id);
        }

        [Test]
        public void should_bulk_delete_v5_blocklist_items()
        {
            var series = TestData.Series();
            var episode = TestData.Episode(series, 1, 1);
            var firstId = SeedBlocklist(series, episode, TestData.NextName("blocklist-one"));
            var secondId = SeedBlocklist(series, episode, TestData.NextName("blocklist-two"));
            var ids = new List<int> { firstId, secondId };

            GetBlocklistPage(seriesIds: new[] { series.Id })
                .Records.Should().Contain(record => record.Id == firstId)
                .And.Contain(record => record.Id == secondId);

            DeleteBlocklistBulk(new V5BlocklistBulkResource { Ids = ids });

            ids.ForEach(WaitForBlocklistItemToDisappear);
            GetBlocklistPage(seriesIds: new[] { series.Id }).Records.Should().NotContain(record => ids.Contains(record.Id));
        }

        private PagingResource<V5HistoryResource> GetHistoryPage(
            int[] eventTypes = null,
            int? episodeId = null,
            string downloadId = null,
            int[] seriesIds = null,
            int[] languages = null,
            int[] quality = null,
            bool includeSubresources = true)
        {
            var request = ApiV5.BuildRequest("history");
            AddPaging(request, "date", "descending");

            AddRepeatedParameter(request, "eventType", eventTypes);
            AddRepeatedParameter(request, "seriesIds", seriesIds);
            AddRepeatedParameter(request, "languages", languages);
            AddRepeatedParameter(request, "quality", quality);

            if (episodeId.HasValue)
            {
                request.AddParameter("episodeId", episodeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(downloadId))
            {
                request.AddParameter("downloadId", downloadId);
            }

            if (includeSubresources)
            {
                request.AddParameter("includeSubresources", "Series");
                request.AddParameter("includeSubresources", "Episode");
            }

            return ApiV5.Execute<PagingResource<V5HistoryResource>>(request);
        }

        private List<V5HistoryResource> GetHistorySince(DateTime date, EpisodeHistoryEventType? eventType = null, bool includeSubresources = false)
        {
            var request = ApiV5.BuildRequest("history/since");
            request.AddParameter("date", date.ToString("o"));
            AddHistoryEventAndSubresources(request, eventType, includeSubresources);

            return ApiV5.Execute<List<V5HistoryResource>>(request);
        }

        private List<V5HistoryResource> GetSeriesHistory(int seriesId, EpisodeHistoryEventType? eventType = null, bool includeSubresources = false)
        {
            var request = ApiV5.BuildRequest("history/series");
            request.AddParameter("seriesId", seriesId);
            AddHistoryEventAndSubresources(request, eventType, includeSubresources);

            return ApiV5.Execute<List<V5HistoryResource>>(request);
        }

        private List<V5HistoryResource> GetSeasonHistory(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType = null, bool includeSubresources = false)
        {
            var request = ApiV5.BuildRequest("history/season");
            request.AddParameter("seriesId", seriesId);
            request.AddParameter("seasonNumber", seasonNumber);
            AddHistoryEventAndSubresources(request, eventType, includeSubresources);

            return ApiV5.Execute<List<V5HistoryResource>>(request);
        }

        private List<V5HistoryResource> GetEpisodeHistory(int episodeId, EpisodeHistoryEventType? eventType = null, bool includeSubresources = false)
        {
            var request = ApiV5.BuildRequest("history/episode");
            request.AddParameter("episodeId", episodeId);
            AddHistoryEventAndSubresources(request, eventType, includeSubresources);

            return ApiV5.Execute<List<V5HistoryResource>>(request);
        }

        private PagingResource<V5EpisodeResource> GetWantedMissing(bool monitored = true, bool includeSeries = false)
        {
            var request = ApiV5.BuildRequest("wanted/missing");
            AddPaging(request, "episodes.airDateUtc", "ascending");
            request.AddParameter("monitored", monitored);

            if (includeSeries)
            {
                request.AddParameter("includeSubresources", "Series");
            }

            return ApiV5.Execute<PagingResource<V5EpisodeResource>>(request);
        }

        private V5EpisodeResource GetWantedMissingById(int id)
        {
            return ApiV5.Execute<V5EpisodeResource>(ApiV5.BuildRequest($"wanted/missing/{id}"));
        }

        private PagingResource<V5EpisodeResource> GetWantedCutoff(bool monitored = true, bool includeSeries = false, bool includeEpisodeFile = false)
        {
            var request = ApiV5.BuildRequest("wanted/cutoff");
            AddPaging(request, "episodes.airDateUtc", "ascending");
            request.AddParameter("monitored", monitored);

            if (includeSeries)
            {
                request.AddParameter("includeSubresources", "Series");
            }

            if (includeEpisodeFile)
            {
                request.AddParameter("includeSubresources", "EpisodeFile");
            }

            return ApiV5.Execute<PagingResource<V5EpisodeResource>>(request);
        }

        private V5EpisodeResource GetWantedCutoffById(int id)
        {
            return ApiV5.Execute<V5EpisodeResource>(ApiV5.BuildRequest($"wanted/cutoff/{id}"));
        }

        private PagingResource<V5BlocklistResource> GetBlocklistPage(int[] seriesIds = null, DownloadProtocol[] protocols = null)
        {
            var request = ApiV5.BuildRequest("blocklist");
            AddPaging(request, "date", "descending");
            AddRepeatedParameter(request, "seriesIds", seriesIds);
            AddRepeatedParameter(request, "protocols", protocols);

            return ApiV5.Execute<PagingResource<V5BlocklistResource>>(request);
        }

        private void DeleteEpisodeFile(int id)
        {
            ApiV5.Delete($"episodefile/{id}", HttpStatusCode.NoContent);
        }

        private void PostHistoryFailed(int id, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            ApiV5.Execute(ApiV5.BuildRequest($"history/failed/{id}", Method.POST), statusCode, authenticated);
        }

        private void DeleteBlocklist(int id)
        {
            ApiV5.Delete($"blocklist/{id}", HttpStatusCode.NoContent);
        }

        private void DeleteBlocklistBulk(V5BlocklistBulkResource resource, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            var request = ApiV5.BuildRequest("blocklist/bulk", Method.DELETE);
            request.AddJsonBody(resource);

            ApiV5.Execute(request, statusCode, authenticated);
        }

        private V5HistoryResource WaitForHistoryRecord(Func<V5HistoryResource, bool> predicate)
        {
            V5HistoryResource record = null;

            WaitForCompletion(
                () =>
                {
                    record = GetHistoryPage().Records.FirstOrDefault(predicate);
                    return record != null;
                },
                30000,
                1000);

            return record;
        }

        private V5BlocklistResource WaitForBlocklistRecord(Func<V5BlocklistResource, bool> predicate)
        {
            V5BlocklistResource record = null;

            WaitForCompletion(
                () =>
                {
                    record = GetBlocklistPage().Records.FirstOrDefault(predicate);
                    return record != null;
                },
                30000,
                1000);

            return record;
        }

        private void WaitForBlocklistItemToDisappear(int id)
        {
            WaitForCompletion(
                () => GetBlocklistPage().Records.All(record => record.Id != id),
                30000,
                1000);
        }

        private int SeedHistory(V3SeriesResource series, V3EpisodeResource episode, EpisodeHistoryEventType eventType, string sourceTitle, string downloadId = null)
        {
            var data = new Dictionary<string, string>
            {
                ["publishedDate"] = DateTime.UtcNow.AddHours(-1).ToString("o"),
                ["size"] = "123456",
                ["protocol"] = ((int)DownloadProtocol.Usenet).ToString(),
                ["indexer"] = "api-test-indexer",
                ["downloadClient"] = "api-test-client",
                ["downloadClientName"] = "api-test-client",
                ["releaseGroup"] = "api-test",
                ["releaseSource"] = "ReleasePush",
                ["indexerFlags"] = "0",
                ["releaseType"] = "0"
            };

            return InsertRow("History", new Dictionary<string, object>
            {
                ["EpisodeId"] = episode.Id,
                ["SeriesId"] = series.Id,
                ["SourceTitle"] = sourceTitle,
                ["Date"] = DateTime.UtcNow,
                ["Quality"] = QualityModelJson(Quality.SDTV),
                ["Data"] = data.ToJson(),
                ["EventType"] = (int)eventType,
                ["DownloadId"] = downloadId,
                ["Languages"] = LanguagesJson(Language.English)
            });
        }

        private int SeedBlocklist(V3SeriesResource series, V3EpisodeResource episode, string sourceTitle)
        {
            return InsertRow("Blocklist", new Dictionary<string, object>
            {
                ["SeriesId"] = series.Id,
                ["EpisodeIds"] = new List<int> { episode.Id }.ToJson(),
                ["SourceTitle"] = sourceTitle,
                ["Quality"] = QualityModelJson(Quality.SDTV),
                ["Date"] = DateTime.UtcNow,
                ["PublishedDate"] = DateTime.UtcNow.AddHours(-1),
                ["Size"] = 123456L,
                ["Protocol"] = (int)DownloadProtocol.Usenet,
                ["Indexer"] = "api-test-indexer",
                ["Message"] = "api test blocklist",
                ["TorrentInfoHash"] = null,
                ["Languages"] = LanguagesJson(Language.English),
                ["IndexerFlags"] = 0,
                ["ReleaseType"] = 0,
                ["Source"] = "api test"
            });
        }

        private int InsertRow(string table, Dictionary<string, object> values)
        {
            using var connection = OpenMainDatabaseConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            var columns = values.Keys.Select(column => $"\"{column}\"").ToList();
            var parameterNames = values.Keys.Select((_, index) => "@p" + index).ToList();
            command.CommandText = $"INSERT INTO \"{table}\" ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameterNames)})";

            if (connection is NpgsqlConnection)
            {
                command.CommandText += " RETURNING \"Id\"";
            }

            var index = 0;
            foreach (var value in values.Values)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@p" + index;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
                index++;
            }

            if (connection is NpgsqlConnection)
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }

            command.ExecuteNonQuery();

            using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid()";

            return Convert.ToInt32(idCommand.ExecuteScalar());
        }

        private DbConnection OpenMainDatabaseConnection()
        {
            if (PostgresOptions?.Host != null)
            {
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = PostgresOptions.Host,
                    Port = PostgresOptions.Port,
                    Username = PostgresOptions.User,
                    Password = PostgresOptions.Password,
                    Database = PostgresOptions.MainDb,
                    Enlist = false,
                    IncludeErrorDetail = true
                };

                return new NpgsqlConnection(builder.ConnectionString);
            }

            var sqliteBuilder = new SQLiteConnectionStringBuilder
            {
                DataSource = Path.Combine(_runner.AppData, "sonarr.db"),
                DateTimeKind = DateTimeKind.Utc,
                Version = 3,
                BusyTimeout = 1000
            };

            return new SQLiteConnection(sqliteBuilder.ConnectionString);
        }

        private static string QualityModelJson(Quality quality)
        {
            return $"{{\"quality\":{quality.Id},\"revision\":{{\"version\":1,\"real\":0,\"isRepack\":false}}}}";
        }

        private static string LanguagesJson(params Language[] languages)
        {
            return $"[{string.Join(",", languages.Select(language => language.Id))}]";
        }

        private static void AddPaging(RestRequest request, string sortKey, string sortDirection)
        {
            request.AddParameter("page", 1);
            request.AddParameter("pageSize", 1000);
            request.AddParameter("sortKey", sortKey);
            request.AddParameter("sortDirection", sortDirection);
        }

        private static void AddHistoryEventAndSubresources(RestRequest request, EpisodeHistoryEventType? eventType, bool includeSubresources)
        {
            if (eventType.HasValue)
            {
                request.AddParameter("eventType", eventType.Value);
            }

            if (includeSubresources)
            {
                request.AddParameter("includeSubresources", "Series");
                request.AddParameter("includeSubresources", "Episode");
            }
        }

        private static void AddRepeatedParameter<T>(RestRequest request, string name, IEnumerable<T> values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                request.AddParameter(name, value);
            }
        }
    }
}
