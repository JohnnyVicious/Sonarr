using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Tv;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using Sonarr.Api.V5;
using Sonarr.Api.V5.SeasonPass;
using V5SeasonResource = Sonarr.Api.V5.Series.SeasonResource;
using V5SeriesEditorResource = Sonarr.Api.V5.Series.SeriesEditorResource;
using V5SeriesResource = Sonarr.Api.V5.Series.SeriesResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class SeriesV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v5_series_and_season_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "series", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "series", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "series/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "series/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "series/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "series/lookup", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "series/editor", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "series/editor", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "series/import", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "series/{id}/folder", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "series/{id}/season", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "seasonpass", HttpStatusCode.NoContent);
        }

        [Test]
        public void should_reject_unauthenticated_v5_series_requests()
        {
            ApiV5.Get("series", HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_return_v5_validation_errors_and_not_found_responses()
        {
            EnsureNoSeries(266189, "The Blacklist");

            var missingProfile = NewSeriesPayload(266189);
            missingProfile.QualityProfileId = 0;

            ApiV5.Post("series", missingProfile, HttpStatusCode.BadRequest)
                .ShouldHaveValidationErrors();

            var missingPath = NewSeriesPayload(266189);
            missingPath.Path = null;
            missingPath.RootFolderPath = null;

            ApiV5.Post("series", missingPath, HttpStatusCode.BadRequest)
                .ShouldHaveValidationErrors();

            ApiV5.Get("series/1000000", HttpStatusCode.NotFound);
        }

        [Test]
        public void should_create_read_update_season_folder_and_delete_v5_series()
        {
            EnsureNoSeries(266189, "The Blacklist");

            var created = CreateSeries(266189);

            try
            {
                created.Id.Should().NotBe(0);
                created.TvdbId.Should().Be(266189);
                created.QualityProfileId.Should().Be(1);
                created.Path.Should().Be(Path.Combine(SeriesRootFolder, created.Title!));

                var all = ListSeries();
                all.Should().Contain(series => series.Id == created.Id && series.TvdbId == 266189);

                var filtered = ListSeries(266189);
                filtered.Should().ContainSingle(series => series.Id == created.Id);

                var byId = GetSeries(created.Id);
                byId.TvdbId.Should().Be(266189);
                byId.Statistics.Should().NotBeNull();

                byId.Monitored = false;
                byId.Seasons.ForEach(season => season.Monitored = false);

                var updated = PutSeries(byId);

                updated.Monitored.Should().BeFalse();
                updated.Seasons.Should().OnlyContain(season => !season.Monitored);
                GetSeries(created.Id).Monitored.Should().BeFalse();

                var season = updated.Seasons.First(season => season.SeasonNumber > 0);
                season.Monitored = true;

                var updatedSeason = PutSeason(created.Id, season);

                updatedSeason.SeasonNumber.Should().Be(season.SeasonNumber);
                updatedSeason.Monitored.Should().BeTrue();
                GetSeries(created.Id).Seasons.Single(s => s.SeasonNumber == season.SeasonNumber).Monitored.Should().BeTrue();

                var folder = ApiV5.Get($"series/{created.Id}/folder")
                    .ShouldHaveJsonObjectContent()["folder"]?.GetValue<string>();

                folder.Should().NotBeNullOrWhiteSpace();

                DeleteSeries(created.Id);

                ApiV5.Get($"series/{created.Id}", HttpStatusCode.NotFound);
                ListSeries().Should().NotContain(series => series.TvdbId == 266189);
            }
            finally
            {
                DeleteSeriesIfPresent(created.Id);
            }
        }

        [Test]
        public void should_lookup_and_import_v5_series()
        {
            EnsureNoSeries(79349, "Dexter");

            var lookup = LookupSeries("tvdb:79349");
            lookup.Should().Contain(series => series.TvdbId == 79349);

            var payload = NewSeriesPayload(79349);
            var imported = PostSeriesImport(new List<V5SeriesResource> { payload }).Single();

            try
            {
                imported.Id.Should().NotBe(0);
                imported.TvdbId.Should().Be(79349);
                GetSeries(imported.Id).TvdbId.Should().Be(79349);
            }
            finally
            {
                DeleteSeriesIfPresent(imported.Id);
            }
        }

        [Test]
        public void should_apply_v5_series_editor_and_seasonpass_changes()
        {
            EnsureNoSeries(266189, "The Blacklist");
            EnsureNoSeries(110381, "Archer (2009)");

            var blacklist = CreateSeries(266189);
            var archer = CreateSeries(110381);

            try
            {
                var tag = TestData.Tag();
                var editorResource = new V5SeriesEditorResource
                {
                    SeriesIds = new List<int> { blacklist.Id, archer.Id },
                    Monitored = false,
                    QualityProfileId = 1,
                    Tags = new List<int> { tag.Id },
                    ApplyTags = ApplyTags.Replace
                };

                var edited = PutSeriesEditor(editorResource);

                edited.Should().HaveCount(2);
                edited.Should().OnlyContain(series => series.Monitored == false);
                edited.Should().OnlyContain(series => series.Tags!.SetEquals(new[] { tag.Id }));

                var refreshed = GetSeries(blacklist.Id);
                var season = refreshed.Seasons.First(s => s.SeasonNumber > 0);
                season.Monitored = false;

                PostSeasonPass(new SeasonPassResource
                {
                    Series = new List<SeasonPassSeriesResource>
                    {
                        new()
                        {
                            Id = refreshed.Id,
                            Monitored = false,
                            Seasons = new List<V5SeasonResource> { season }
                        }
                    }
                });

                var seasonPassResult = GetSeries(refreshed.Id);
                seasonPassResult.Monitored.Should().BeFalse();
                seasonPassResult.Seasons.Single(s => s.SeasonNumber == season.SeasonNumber).Monitored.Should().BeFalse();

                DeleteSeriesEditor(new V5SeriesEditorResource
                {
                    SeriesIds = new List<int> { blacklist.Id, archer.Id }
                });

                ApiV5.Get($"series/{blacklist.Id}", HttpStatusCode.NotFound);
                ApiV5.Get($"series/{archer.Id}", HttpStatusCode.NotFound);
            }
            finally
            {
                DeleteSeriesIfPresent(blacklist.Id);
                DeleteSeriesIfPresent(archer.Id);
            }
        }

        private List<V5SeriesResource> LookupSeries(string term)
        {
            var request = ApiV5.BuildRequest("series/lookup");
            request.AddParameter("term", term);

            return ApiV5.Execute<List<V5SeriesResource>>(request);
        }

        private List<V5SeriesResource> ListSeries(int? tvdbId = null)
        {
            var request = ApiV5.BuildRequest("series");

            if (tvdbId.HasValue)
            {
                request.AddParameter("tvdbId", tvdbId.Value);
            }

            return ApiV5.Execute<List<V5SeriesResource>>(request);
        }

        private V5SeriesResource GetSeries(int id)
        {
            return ApiV5.Execute<V5SeriesResource>(ApiV5.BuildRequest($"series/{id}"));
        }

        private V5SeriesResource NewSeriesPayload(int tvdbId, bool monitored = true)
        {
            var series = LookupSeries("tvdb:" + tvdbId).Single(s => s.TvdbId == tvdbId);
            series.QualityProfileId = TestData.QualityProfile().Id;
            series.Path = Path.Combine(SeriesRootFolder, series.Title!);
            series.Monitored = monitored;
            series.Seasons.ForEach(season => season.Monitored = monitored);
            series.AddOptions = new AddSeriesOptions();

            Directory.CreateDirectory(series.Path);

            return series;
        }

        private V5SeriesResource CreateSeries(int tvdbId, bool monitored = true)
        {
            var created = PostSeries(NewSeriesPayload(tvdbId, monitored));

            Commands.WaitAll();
            WaitForCompletion(() => Episodes.GetEpisodesInSeries(created.Id).Count > 0);

            return GetSeries(created.Id);
        }

        private V5SeriesResource PostSeries(V5SeriesResource series)
        {
            return Read<V5SeriesResource>(ApiV5.Post("series", series));
        }

        private V5SeriesResource PutSeries(V5SeriesResource series)
        {
            return Read<V5SeriesResource>(ApiV5.Put($"series/{series.Id}", series));
        }

        private V5SeasonResource PutSeason(int seriesId, V5SeasonResource season)
        {
            return Read<V5SeasonResource>(ApiV5.Put($"series/{seriesId}/season", season, HttpStatusCode.OK));
        }

        private List<V5SeriesResource> PutSeriesEditor(V5SeriesEditorResource resource)
        {
            return Read<List<V5SeriesResource>>(ApiV5.Put("series/editor", resource, HttpStatusCode.OK));
        }

        private List<V5SeriesResource> PostSeriesImport(List<V5SeriesResource> resource)
        {
            return Read<List<V5SeriesResource>>(ApiV5.Post("series/import", resource, HttpStatusCode.OK));
        }

        private void PostSeasonPass(SeasonPassResource resource)
        {
            ApiV5.Post("seasonpass", resource, HttpStatusCode.NoContent);
        }

        private void DeleteSeries(int id)
        {
            ApiV5.Delete($"series/{id}", HttpStatusCode.NoContent);
        }

        private void DeleteSeriesIfPresent(int id)
        {
            if (id == 0)
            {
                return;
            }

            if (ListSeries().Any(series => series.Id == id))
            {
                DeleteSeries(id);
            }
        }

        private void DeleteSeriesEditor(V5SeriesEditorResource resource)
        {
            var request = ApiV5.BuildRequest("series/editor", Method.DELETE);
            request.AddJsonBody(resource);

            ApiV5.Execute(request, HttpStatusCode.NoContent);
        }

        private static T Read<T>(IRestResponse response)
            where T : new()
        {
            response.ShouldHaveJsonContent();

            return Json.Deserialize<T>(response.Content);
        }
    }
}
