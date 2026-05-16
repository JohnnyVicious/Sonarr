using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Integration.Test.Client;
using Sonarr.Api.V3.DownloadClient;
using Sonarr.Api.V3.EpisodeFiles;
using Sonarr.Api.V3.Episodes;
using Sonarr.Api.V3.History;
using Sonarr.Api.V3.Indexers;
using Sonarr.Api.V3.Profiles.Quality;
using Sonarr.Api.V3.Queue;
using Sonarr.Api.V3.RootFolders;
using Sonarr.Api.V3.Series;
using Sonarr.Api.V3.Tags;
using Sonarr.Http;

namespace NzbDrone.Integration.Test.ApiTests
{
    public sealed class ApiTestData
    {
        private readonly IntegrationTestBase _test;
        private readonly ApiTestDataNames _names;
        private readonly List<Action> _cleanup;

        public ApiTestData(IntegrationTestBase test, string testName)
        {
            _test = test;
            _names = new ApiTestDataNames(testName);
            _cleanup = new List<Action>();
        }

        public string NextName(string stem)
        {
            return _names.Next(stem);
        }

        public RootFolderResource RootFolder(string name = null)
        {
            var path = _test.GetTempDirectory("RootFolders", name == null ? NextName("root") : SafePathSegment(name, nameof(name)));
            var rootFolder = _test.RootFolders.Post(new RootFolderResource { Path = path });

            Track(() => DeleteRootFolderIfPresent(rootFolder.Id));

            return rootFolder;
        }

        public QualityProfileResource QualityProfile(int id = 1)
        {
            return _test.QualityProfiles.Get(id);
        }

        public QualityProfileResource QualityProfileCutoff(Quality cutoff, bool upgradeAllowed = true, int profileId = 1)
        {
            var original = _test.QualityProfiles.Get(profileId);
            var profile = _test.EnsureQualityProfileCutoff(profileId, cutoff, upgradeAllowed);

            Track(() => _test.QualityProfiles.Put(original));

            return profile;
        }

        public TagResource Tag(string label = null)
        {
            var tag = _test.Tags.Post(new TagResource { Label = label ?? NextName("tag") });

            Track(() => DeleteTagIfPresent(tag.Id));

            return tag;
        }

        public SeriesResource Series(int tvdbId = 266189, string title = "The Blacklist", bool monitored = true, params TagResource[] tags)
        {
            DeleteSeriesWithTvdbIfPresent(tvdbId);

            var series = _test.Series.Lookup("tvdb:" + tvdbId).First();
            if (!series.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected TVDB {tvdbId} to resolve to {title}, got {series.Title}.");
            }

            var rootFolder = _test.SeriesRootFolder;

            series.QualityProfileId = QualityProfile().Id;
            series.Path = Path.Combine(rootFolder, series.Title);
            series.Monitored = monitored;
            series.Seasons.ForEach(season => season.Monitored = monitored);
            series.Tags = tags.Select(tag => tag.Id).ToHashSet();
            series.AddOptions = new AddSeriesOptions();

            Directory.CreateDirectory(series.Path);

            var created = _test.Series.Post(series);
            _test.Commands.WaitAll();
            IntegrationTestBase.WaitForCompletion(() => _test.Episodes.GetEpisodesInSeries(created.Id).Count > 0);

            Track(() => DeleteSeriesIfPresent(created.Id));

            return created;
        }

        public List<EpisodeResource> Episodes(SeriesResource series)
        {
            return _test.Episodes.GetEpisodesInSeries(series.Id);
        }

        public EpisodeResource Episode(SeriesResource series, int seasonNumber = 1, int episodeNumber = 1)
        {
            return Episodes(series).Single(episode => episode.SeasonNumber == seasonNumber && episode.EpisodeNumber == episodeNumber);
        }

        public EpisodeFileResource EpisodeFile(SeriesResource series, int seasonNumber = 1, int episodeNumber = 1, Quality quality = null)
        {
            return _test.EnsureEpisodeFile(series, seasonNumber, episodeNumber, quality ?? Quality.SDTV);
        }

        public DownloadClientResource DownloadClient(bool enabled = true)
        {
            var schema = DownloadClientSchema("UsenetBlackhole");

            schema.Enable = enabled;
            schema.Name = NextName("usenet-blackhole");
            schema.Fields.First(field => field.Name == "watchFolder").Value = _test.GetTempDirectory("Download", schema.Name, "Watch");
            schema.Fields.First(field => field.Name == "nzbFolder").Value = _test.GetTempDirectory("Download", schema.Name, "Nzb");

            var client = _test.DownloadClients.Post(schema);

            Track(() => DeleteDownloadClientIfPresent(client.Id));

            return client;
        }

        public DownloadClientResource DownloadClientSchema(string implementation)
        {
            return _test.DownloadClients.Schema().First(schema => schema.Implementation == implementation);
        }

        public IndexerResource IndexerSchema(string implementation = "Newznab")
        {
            return _test.Indexers.Schema().First(schema => schema.Implementation == implementation);
        }

        public QueueResource QueuedDownload(string fileName = "Series.Title.S01E01.mkv")
        {
            var safeFileName = SafePathSegment(fileName, nameof(fileName));
            var client = DownloadClient();
            var watchFolder = client.Fields.First(field => field.Name == "watchFolder").Value as string;
            if (watchFolder == null)
            {
                throw new InvalidOperationException("UsenetBlackhole schema did not produce a watchFolder path.");
            }

            var filePath = Path.Combine(watchFolder, safeFileName);
            QueueResource queuedDownload = null;

            File.WriteAllText(filePath, "Test Download");
            RefreshMonitoredDownloads();

            IntegrationTestBase.WaitForCompletion(
                () =>
                {
                    queuedDownload = QueuePage(includeUnknownSeriesItems: true)
                        .Records
                        .FirstOrDefault(record => IsQueuedDownload(record, filePath, safeFileName));

                    return queuedDownload != null;
                },
                30000,
                1000);

            return queuedDownload;
        }

        public PagingResource<QueueResource> QueuePage(bool includeUnknownSeriesItems = false)
        {
            var request = _test.Queue.BuildRequest();

            if (includeUnknownSeriesItems)
            {
                request.AddParameter("includeUnknownSeriesItems", true);
            }

            return _test.Queue.Get<PagingResource<QueueResource>>(request);
        }

        public PagingResource<HistoryResource> HistoryPage(params int[] seriesIds)
        {
            var request = _test.History.BuildRequest();
            request.AddParameter("page", 1);
            request.AddParameter("pageSize", 100);
            request.AddParameter("sortKey", "date");
            request.AddParameter("sortDir", "descending");

            foreach (var seriesId in seriesIds)
            {
                request.AddParameter("seriesIds", seriesId);
            }

            return _test.History.Get<PagingResource<HistoryResource>>(request);
        }

        public void Cleanup()
        {
            var failures = new List<Exception>();

            foreach (var cleanup in Enumerable.Reverse(_cleanup))
            {
                try
                {
                    cleanup();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            _cleanup.Clear();

            if (failures.Any())
            {
                throw new AggregateException("API test data cleanup failed.", failures);
            }
        }

        private void DeleteRootFolderIfPresent(int id)
        {
            _test.RootFolders.DeleteIfPresent(id);
        }

        private void DeleteTagIfPresent(int id)
        {
            _test.Tags.DeleteIfPresent(id);
        }

        private void DeleteSeriesIfPresent(int id)
        {
            _test.Series.DeleteIfPresent(id);
        }

        private void DeleteSeriesWithTvdbIfPresent(int tvdbId)
        {
            var existing = _test.Series.All().FirstOrDefault(series => series.TvdbId == tvdbId);

            if (existing != null)
            {
                _test.Series.Delete(existing.Id);
            }
        }

        private void DeleteDownloadClientIfPresent(int id)
        {
            _test.DownloadClients.DeleteIfPresent(id);
        }

        private void RefreshMonitoredDownloads()
        {
            var command = _test.Commands.Post(new SimpleCommandResource { Name = "RefreshMonitoredDownloads" });

            IntegrationTestBase.WaitForCompletion(
                () =>
                {
                    var updatedCommand = _test.Commands.Get(command.Id);

                    if (updatedCommand.Status is CommandStatus.Failed or CommandStatus.Aborted or CommandStatus.Cancelled or CommandStatus.Orphaned)
                    {
                        throw new InvalidOperationException($"RefreshMonitoredDownloads finished with {updatedCommand.Status}.");
                    }

                    if (updatedCommand.Status == CommandStatus.Completed)
                    {
                        return true;
                    }

                    return false;
                },
                30000,
                1000);
        }

        private static bool IsQueuedDownload(QueueResource record, string filePath, string fileName)
        {
            var title = Path.GetFileNameWithoutExtension(fileName);

            return string.Equals(record.OutputPath, filePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(record.Title, fileName, StringComparison.OrdinalIgnoreCase) ||
                   (record.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void Track(Action cleanup)
        {
            _cleanup.Add(cleanup);
        }

        internal static string SafePathSegment(string value, string parameterName)
        {
            var segment = value?.Trim();

            if (string.IsNullOrWhiteSpace(segment) ||
                segment == "." ||
                segment == ".." ||
                Path.IsPathRooted(segment) ||
                segment.Contains('/') ||
                segment.Contains('\\') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("API test data paths must be single relative path segments.", parameterName);
            }

            return segment;
        }
    }

    public sealed class ApiTestDataNames
    {
        private readonly string _prefix;
        private int _counter;

        public ApiTestDataNames(string testName)
        {
            _prefix = Slug(testName, 48);
        }

        public string Next(string stem)
        {
            _counter++;

            return $"api-{Slug(stem, 32)}-{_prefix}-{_counter:D2}";
        }

        private static string Slug(string value, int maxLength)
        {
            value ??= string.Empty;

            var chars = value
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();
            var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));

            if (slug.Length > maxLength)
            {
                slug = slug.Substring(0, maxLength).TrimEnd('-');
            }

            return string.IsNullOrWhiteSpace(slug) ? "data" : slug;
        }
    }
}
