using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using V5EpisodeFileListResource = Sonarr.Api.V5.EpisodeFiles.EpisodeFileListResource;
using V5EpisodeFileResource = Sonarr.Api.V5.EpisodeFiles.EpisodeFileResource;
using V5EpisodeResource = Sonarr.Api.V5.Episodes.EpisodeResource;
using V5EpisodesMonitoredResource = Sonarr.Api.V5.Episodes.EpisodesMonitoredResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class EpisodeV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v5_episode_and_episodefile_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episode", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episode", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episode/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episode/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "episode/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "episode/monitor", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episodefile", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episodefile", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episodefile/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "episodefile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "episodefile/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "episodefile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "episodefile/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "episodefile/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "episodefile/bulk", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "episodefile/bulk", HttpStatusCode.NoContent);
        }

        [Test]
        public void should_reject_unauthenticated_v5_episode_requests()
        {
            ApiV5.Get("episode?seriesId=1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("episode/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("episode/1", new V5EpisodeResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("episode/monitor", new V5EpisodesMonitoredResource { EpisodeIds = new List<int> { 1 }, Monitored = false }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("episodefile?seriesId=1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("episodefile/1", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Put("episodefile/1", new V5EpisodeFileResource { Id = 1 }, HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Delete("episodefile/1", HttpStatusCode.Unauthorized, authenticated: false);
            PutEpisodeFilesBulk(new List<V5EpisodeFileResource> { new() { Id = 1 } }, HttpStatusCode.Unauthorized, authenticated: false);
            DeleteEpisodeFilesBulk(new V5EpisodeFileListResource { EpisodeFileIds = new List<int> { 1 } }, HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_query_and_monitor_v5_episodes()
        {
            var series = TestData.Series();
            var episodes = ListEpisodes(series.Id);
            episodes.Should().NotBeEmpty();

            var first = episodes.First(episode => episode.SeasonNumber > 0);
            var byId = GetEpisode(first.Id);
            byId.SeriesId.Should().Be(series.Id);
            byId.Id.Should().Be(first.Id);

            ListEpisodesByIds(first.Id).Should().ContainSingle(episode => episode.Id == first.Id);
            ListEpisodes(series.Id, first.SeasonNumber).Should().OnlyContain(episode => episode.SeasonNumber == first.SeasonNumber);

            ApiV5.Get("episode", HttpStatusCode.BadRequest);
            ApiV5.Get("episode/1000000", HttpStatusCode.NotFound);
            ApiV5.Put("episode/0", new V5EpisodeResource { Id = 0 }, HttpStatusCode.BadRequest);

            first.Monitored = false;
            PutEpisode(first).Monitored.Should().BeFalse();
            GetEpisode(first.Id).Monitored.Should().BeFalse();

            var monitorTargets = episodes.Where(episode => episode.SeasonNumber > 0).Take(2).ToList();
            monitorTargets.Should().HaveCount(2);

            var monitored = PutEpisodeMonitor(new V5EpisodesMonitoredResource
            {
                EpisodeIds = monitorTargets.Select(episode => episode.Id).ToList(),
                Monitored = true
            });

            monitored.Should().HaveCount(2);
            monitored.Should().OnlyContain(episode => episode.Monitored);
            ListEpisodesByIds(monitorTargets.Select(episode => episode.Id).ToArray()).Should().OnlyContain(episode => episode.Monitored);
        }

        [Test]
        public void should_read_update_and_delete_v5_episode_files()
        {
            var series = TestData.Series();
            var episodes = TestData.Episodes(series).Where(episode => episode.SeasonNumber > 0).Take(2).ToList();
            episodes.Should().HaveCount(2);

            var firstFile = TestData.EpisodeFile(series, episodes[0].SeasonNumber, episodes[0].EpisodeNumber, Quality.SDTV);
            var secondFile = TestData.EpisodeFile(series, episodes[1].SeasonNumber, episodes[1].EpisodeNumber, Quality.SDTV);

            try
            {
                var first = GetEpisodeFile(firstFile.Id);
                var second = GetEpisodeFile(secondFile.Id);

                first.SeriesId.Should().Be(series.Id);
                first.Quality!.Quality.Id.Should().Be(Quality.SDTV.Id);
                ListEpisodeFiles(series.Id).Should().Contain(file => file.Id == first.Id);
                ListEpisodeFilesByIds(first.Id).Should().ContainSingle(file => file.Id == first.Id);
                ListEpisodesByFileId(first.Id).Should().ContainSingle(episode => episode.EpisodeFileId == first.Id);

                ApiV5.Get("episodefile", HttpStatusCode.BadRequest);
                ApiV5.Get("episodefile/1000000", HttpStatusCode.NotFound);
                ApiV5.Put("episodefile/0", new V5EpisodeFileResource { Id = 0 }, HttpStatusCode.BadRequest);
                ApiV5.Delete("episodefile/0", HttpStatusCode.BadRequest);

                first.ReleaseGroup = "single-v5";
                PutEpisodeFile(first).ReleaseGroup.Should().Be("single-v5");
                GetEpisodeFile(first.Id).ReleaseGroup.Should().Be("single-v5");

                first.ReleaseGroup = "bulk-v5";
                second.ReleaseGroup = "bulk-v5";
                first.Languages = new List<Language> { Language.English };
                second.Languages = new List<Language> { Language.English };

                var bulkUpdated = PutEpisodeFilesBulk(new List<V5EpisodeFileResource> { first, second });

                bulkUpdated.Should().HaveCount(2);
                bulkUpdated.Should().OnlyContain(file => file.ReleaseGroup == "bulk-v5");
                bulkUpdated.Should().OnlyContain(file => file.Languages.Contains(Language.English));
                ListEpisodeFilesByIds(first.Id, second.Id).Should().OnlyContain(file => file.ReleaseGroup == "bulk-v5");

                DeleteEpisodeFile(first.Id);
                ApiV5.Get($"episodefile/{first.Id}", HttpStatusCode.NotFound);

                DeleteEpisodeFilesBulk(new V5EpisodeFileListResource { EpisodeFileIds = new List<int> { second.Id } });
                ApiV5.Get($"episodefile/{second.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                DeleteEpisodeFileIfPresent(firstFile.Id);
                DeleteEpisodeFileIfPresent(secondFile.Id);
            }
        }

        private List<V5EpisodeResource> ListEpisodes(int seriesId, int? seasonNumber = null)
        {
            var request = ApiV5.BuildRequest("episode");
            request.AddParameter("seriesId", seriesId);

            if (seasonNumber.HasValue)
            {
                request.AddParameter("seasonNumber", seasonNumber.Value);
            }

            return ApiV5.Execute<List<V5EpisodeResource>>(request);
        }

        private List<V5EpisodeResource> ListEpisodesByIds(params int[] episodeIds)
        {
            var request = ApiV5.BuildRequest("episode");

            foreach (var episodeId in episodeIds)
            {
                request.AddParameter("episodeIds", episodeId);
            }

            return ApiV5.Execute<List<V5EpisodeResource>>(request);
        }

        private List<V5EpisodeResource> ListEpisodesByFileId(int episodeFileId)
        {
            var request = ApiV5.BuildRequest("episode");
            request.AddParameter("episodeFileId", episodeFileId);

            return ApiV5.Execute<List<V5EpisodeResource>>(request);
        }

        private V5EpisodeResource GetEpisode(int id)
        {
            return ApiV5.Execute<V5EpisodeResource>(ApiV5.BuildRequest($"episode/{id}"));
        }

        private V5EpisodeResource PutEpisode(V5EpisodeResource episode)
        {
            return Read<V5EpisodeResource>(ApiV5.Put($"episode/{episode.Id}", episode, HttpStatusCode.OK));
        }

        private List<V5EpisodeResource> PutEpisodeMonitor(V5EpisodesMonitoredResource resource)
        {
            return Read<List<V5EpisodeResource>>(ApiV5.Put("episode/monitor", resource, HttpStatusCode.OK));
        }

        private List<V5EpisodeFileResource> ListEpisodeFiles(int seriesId)
        {
            var request = ApiV5.BuildRequest("episodefile");
            request.AddParameter("seriesId", seriesId);

            return ApiV5.Execute<List<V5EpisodeFileResource>>(request);
        }

        private List<V5EpisodeFileResource> ListEpisodeFilesByIds(params int[] episodeFileIds)
        {
            var request = ApiV5.BuildRequest("episodefile");

            foreach (var episodeFileId in episodeFileIds)
            {
                request.AddParameter("episodeFileIds", episodeFileId);
            }

            return ApiV5.Execute<List<V5EpisodeFileResource>>(request);
        }

        private V5EpisodeFileResource GetEpisodeFile(int id)
        {
            return ApiV5.Execute<V5EpisodeFileResource>(ApiV5.BuildRequest($"episodefile/{id}"));
        }

        private V5EpisodeFileResource PutEpisodeFile(V5EpisodeFileResource episodeFile)
        {
            return Read<V5EpisodeFileResource>(ApiV5.Put($"episodefile/{episodeFile.Id}", episodeFile));
        }

        private List<V5EpisodeFileResource> PutEpisodeFilesBulk(List<V5EpisodeFileResource> episodeFiles, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            return ReadOptional<List<V5EpisodeFileResource>>(ApiV5.Put("episodefile/bulk", episodeFiles, statusCode, authenticated));
        }

        private void DeleteEpisodeFile(int id)
        {
            ApiV5.Delete($"episodefile/{id}", HttpStatusCode.NoContent);
        }

        private void DeleteEpisodeFileIfPresent(int id)
        {
            if (id == 0)
            {
                return;
            }

            if (ListEpisodeFilesByIds(id).Any(file => file.Id == id))
            {
                DeleteEpisodeFile(id);
            }
        }

        private void DeleteEpisodeFilesBulk(V5EpisodeFileListResource resource, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            var request = ApiV5.BuildRequest("episodefile/bulk", Method.DELETE);
            request.AddJsonBody(resource);

            ApiV5.Execute(request, statusCode, authenticated);
        }

        private static T Read<T>(IRestResponse response)
            where T : new()
        {
            response.ShouldHaveJsonContent();

            return Json.Deserialize<T>(response.Content);
        }

        private static T ReadOptional<T>(IRestResponse response)
            where T : new()
        {
            if (response.ContentLength == 0 || string.IsNullOrWhiteSpace(response.Content))
            {
                return new T();
            }

            return Read<T>(response);
        }
    }
}
