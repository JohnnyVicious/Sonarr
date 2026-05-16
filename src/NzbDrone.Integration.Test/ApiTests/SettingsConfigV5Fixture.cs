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
using JsonArray = System.Text.Json.Nodes.JsonArray;
using V3DownloadClientConfigResource = Sonarr.Api.V3.Config.DownloadClientConfigResource;
using V3HostConfigResource = Sonarr.Api.V3.Config.HostConfigResource;
using V3ImportListConfigResource = Sonarr.Api.V3.Config.ImportListConfigResource;
using V3IndexerConfigResource = Sonarr.Api.V3.Config.IndexerConfigResource;
using V3MediaManagementConfigResource = Sonarr.Api.V3.Config.MediaManagementConfigResource;
using V3NamingConfigResource = Sonarr.Api.V3.Config.NamingConfigResource;
using V3UiConfigResource = Sonarr.Api.V3.Config.UiConfigResource;
using V5GeneralSettingsResource = Sonarr.Api.V5.Settings.GeneralSettingsResource;
using V5IndexerSettingsResource = Sonarr.Api.V5.Settings.IndexerSettingsResource;
using V5MediaManagementSettingsResource = Sonarr.Api.V5.Settings.MediaManagementSettingsResource;
using V5NamingSettingsResource = Sonarr.Api.V5.Settings.NamingSettingsResource;
using V5UiSettingsResource = Sonarr.Api.V5.Settings.UiSettingsResource;
using V5UpdateSettingsResource = Sonarr.Api.V5.Settings.UpdateSettingsResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class SettingsConfigV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v5_settings_and_v3_config_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/general", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/general/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/general/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/mediamanagement", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/mediamanagement/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/mediamanagement/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/naming", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/naming/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/naming/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/naming/examples", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/indexer", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/indexer/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/indexer/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/ui", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/ui/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/ui/{id}", HttpStatusCode.Accepted);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/update", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "settings/update/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.PUT, "settings/update/{id}", HttpStatusCode.Accepted);

            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/downloadclient", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/downloadclient/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/downloadclient/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/host", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/host/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/host/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/importlist", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/importlist/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/importlist/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/indexer", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/indexer/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/indexer/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/mediamanagement", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/mediamanagement/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/mediamanagement/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/naming", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/naming/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/naming/{id}");
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/naming/examples", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/ui", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareResponse(Method.GET, "config/ui/{id}", HttpStatusCode.OK);
            ApiV3.OpenApi.ShouldDeclareOperation(Method.PUT, "config/ui/{id}");
        }

        [Test]
        public void should_reject_unauthenticated_v5_settings_and_v3_config_requests()
        {
            foreach (var resource in V5SettingsResources())
            {
                ApiV5.Get(resource, HttpStatusCode.Unauthorized, authenticated: false);
                ApiV5.Get($"{resource}/1", HttpStatusCode.Unauthorized, authenticated: false);
                ApiV5.Put($"{resource}/1", new object(), HttpStatusCode.Unauthorized, authenticated: false);
            }

            ApiV5.Get("settings/naming/examples", HttpStatusCode.Unauthorized, authenticated: false);

            foreach (var resource in V3ConfigResources())
            {
                ApiV3.Get(resource, HttpStatusCode.Unauthorized, authenticated: false);
                ApiV3.Get($"{resource}/1", HttpStatusCode.Unauthorized, authenticated: false);
                ApiV3.Put($"{resource}/1", new object(), HttpStatusCode.Unauthorized, authenticated: false);
            }

            ApiV3.Get("config/naming/examples", HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_read_update_restore_and_persist_v5_settings()
        {
            var general = Copy(GetV5<V5GeneralSettingsResource>("settings/general"));
            var media = Copy(GetV5<V5MediaManagementSettingsResource>("settings/mediamanagement"));
            var naming = Copy(GetV5<V5NamingSettingsResource>("settings/naming"));
            var indexer = Copy(GetV5<V5IndexerSettingsResource>("settings/indexer"));
            var ui = Copy(GetV5<V5UiSettingsResource>("settings/ui"));
            var update = Copy(GetV5<V5UpdateSettingsResource>("settings/update"));

            try
            {
                var updatedGeneral = Copy(general);
                updatedGeneral.AnalyticsEnabled = !general.AnalyticsEnabled;
                PutV5("settings/general", updatedGeneral);
                GetV5<V5GeneralSettingsResource>("settings/general").AnalyticsEnabled.Should().Be(updatedGeneral.AnalyticsEnabled);

                var updatedMedia = Copy(media);
                updatedMedia.CreateEmptySeriesFolders = !media.CreateEmptySeriesFolders;
                PutV5("settings/mediamanagement", updatedMedia);
                GetV5<V5MediaManagementSettingsResource>("settings/mediamanagement").CreateEmptySeriesFolders.Should().Be(updatedMedia.CreateEmptySeriesFolders);

                var updatedNaming = Copy(naming);
                updatedNaming.RenameEpisodes = !naming.RenameEpisodes;
                PutV5("settings/naming", updatedNaming);
                GetV5<V5NamingSettingsResource>("settings/naming").RenameEpisodes.Should().Be(updatedNaming.RenameEpisodes);
                ApiV5.Get("settings/naming/examples").ShouldHaveJsonObjectContent();

                var updatedIndexer = Copy(indexer);
                updatedIndexer.MinimumAge = indexer.MinimumAge + 1;
                PutV5("settings/indexer", updatedIndexer);
                GetV5<V5IndexerSettingsResource>("settings/indexer").MinimumAge.Should().Be(updatedIndexer.MinimumAge);

                var updatedUi = Copy(ui);
                updatedUi.ShowRelativeDates = !ui.ShowRelativeDates;
                PutV5("settings/ui", updatedUi);
                GetV5<V5UiSettingsResource>("settings/ui").ShowRelativeDates.Should().Be(updatedUi.ShowRelativeDates);

                var updatedUpdate = Copy(update);
                updatedUpdate.UpdateAutomatically = !update.UpdateAutomatically;
                PutV5("settings/update", updatedUpdate);
                GetV5<V5UpdateSettingsResource>("settings/update").UpdateAutomatically.Should().Be(updatedUpdate.UpdateAutomatically);
            }
            finally
            {
                PutV5("settings/update", update);
                PutV5("settings/ui", ui);
                PutV5("settings/indexer", indexer);
                PutV5("settings/naming", naming);
                PutV5("settings/mediamanagement", media);
                PutV5("settings/general", general);
            }
        }

        [Test]
        public void should_read_update_restore_and_persist_v3_config_routes()
        {
            var downloadClient = Copy(GetV3<V3DownloadClientConfigResource>("config/downloadclient"));
            var host = Copy(GetV3<V3HostConfigResource>("config/host"));
            var importList = Copy(GetV3<V3ImportListConfigResource>("config/importlist"));
            var indexer = Copy(GetV3<V3IndexerConfigResource>("config/indexer"));
            var media = Copy(GetV3<V3MediaManagementConfigResource>("config/mediamanagement"));
            var naming = Copy(GetV3<V3NamingConfigResource>("config/naming"));
            var ui = Copy(GetV3<V3UiConfigResource>("config/ui"));

            try
            {
                var updatedDownloadClient = Copy(downloadClient);
                updatedDownloadClient.AutoRedownloadFailed = !downloadClient.AutoRedownloadFailed;
                PutV3("config/downloadclient", updatedDownloadClient);
                GetV3<V3DownloadClientConfigResource>("config/downloadclient").AutoRedownloadFailed.Should().Be(updatedDownloadClient.AutoRedownloadFailed);

                var updatedHost = Copy(host);
                updatedHost.AnalyticsEnabled = !host.AnalyticsEnabled;
                PutV3("config/host", updatedHost);
                GetV3<V3HostConfigResource>("config/host").AnalyticsEnabled.Should().Be(updatedHost.AnalyticsEnabled);

                var updatedImportList = Copy(importList);
                updatedImportList.ListSyncTag = importList.ListSyncTag == 0 ? 1 : 0;
                PutV3("config/importlist", updatedImportList);
                GetV3<V3ImportListConfigResource>("config/importlist").ListSyncTag.Should().Be(updatedImportList.ListSyncTag);

                var updatedIndexer = Copy(indexer);
                updatedIndexer.Retention = indexer.Retention + 1;
                PutV3("config/indexer", updatedIndexer);
                GetV3<V3IndexerConfigResource>("config/indexer").Retention.Should().Be(updatedIndexer.Retention);

                var updatedMedia = Copy(media);
                updatedMedia.DeleteEmptyFolders = !media.DeleteEmptyFolders;
                PutV3("config/mediamanagement", updatedMedia);
                GetV3<V3MediaManagementConfigResource>("config/mediamanagement").DeleteEmptyFolders.Should().Be(updatedMedia.DeleteEmptyFolders);

                var updatedNaming = Copy(naming);
                updatedNaming.ReplaceIllegalCharacters = !naming.ReplaceIllegalCharacters;
                PutV3("config/naming", updatedNaming);
                GetV3<V3NamingConfigResource>("config/naming").ReplaceIllegalCharacters.Should().Be(updatedNaming.ReplaceIllegalCharacters);
                ApiV3.Get("config/naming/examples").ShouldHaveJsonObjectContent();

                var updatedUi = Copy(ui);
                updatedUi.EnableColorImpairedMode = !ui.EnableColorImpairedMode;
                PutV3("config/ui", updatedUi);
                GetV3<V3UiConfigResource>("config/ui").EnableColorImpairedMode.Should().Be(updatedUi.EnableColorImpairedMode);
            }
            finally
            {
                PutV3("config/ui", ui);
                PutV3("config/naming", naming);
                PutV3("config/mediamanagement", media);
                PutV3("config/indexer", indexer);
                PutV3("config/importlist", importList);
                PutV3("config/host", host);
                PutV3("config/downloadclient", downloadClient);
            }
        }

        [Test]
        public void should_validate_invalid_v5_settings_and_v3_config_values()
        {
            var portGeneral = Copy(GetV5<V5GeneralSettingsResource>("settings/general"));
            portGeneral.Port = 0;
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/general/1", portGeneral, HttpStatusCode.BadRequest), "port");

            var urlGeneral = Copy(GetV5<V5GeneralSettingsResource>("settings/general"));
            urlGeneral.UrlBase = "invalid url base";
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/general/1", urlGeneral, HttpStatusCode.BadRequest), "urlBase");

            var authGeneral = Copy(GetV5<V5GeneralSettingsResource>("settings/general"));
            authGeneral.Username = string.Empty;
            authGeneral.Password = string.Empty;
            authGeneral.PasswordConfirmation = string.Empty;
            authGeneral.AuthenticationMethod = NzbDrone.Core.Authentication.AuthenticationType.Forms;
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/general/1", authGeneral, HttpStatusCode.BadRequest), "username");

            var media = Copy(GetV5<V5MediaManagementSettingsResource>("settings/mediamanagement"));
            media.RecycleBin = Path.Combine(TempDirectory, "missing-recycle-bin");
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/mediamanagement/1", media, HttpStatusCode.BadRequest), "recycleBin");

            var indexer = Copy(GetV5<V5IndexerSettingsResource>("settings/indexer"));
            indexer.RssSyncInterval = 1;
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/indexer/1", indexer, HttpStatusCode.BadRequest), "rssSyncInterval");

            var naming = Copy(GetV5<V5NamingSettingsResource>("settings/naming"));
            naming.MultiEpisodeStyle = 99;
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/naming/1", naming, HttpStatusCode.BadRequest), "multiEpisodeStyle");

            var ui = Copy(GetV5<V5UiSettingsResource>("settings/ui"));
            ui.UiLanguage = 0;
            ShouldHaveValidationErrorFor(ApiV5.Put("settings/ui/1", ui, HttpStatusCode.BadRequest), "uiLanguage");

            var host = Copy(GetV3<V3HostConfigResource>("config/host"));
            host.Port = 0;
            ShouldHaveValidationErrorFor(ApiV3.Put("config/host/1", host, HttpStatusCode.BadRequest), "port");

            var v3Naming = Copy(GetV3<V3NamingConfigResource>("config/naming"));
            v3Naming.MultiEpisodeStyle = 99;
            ShouldHaveValidationErrorFor(ApiV3.Put("config/naming/1", v3Naming, HttpStatusCode.BadRequest), "multiEpisodeStyle");
        }

        private static IEnumerable<string> V5SettingsResources()
        {
            yield return "settings/general";
            yield return "settings/mediamanagement";
            yield return "settings/naming";
            yield return "settings/indexer";
            yield return "settings/ui";
            yield return "settings/update";
        }

        private static IEnumerable<string> V3ConfigResources()
        {
            yield return "config/downloadclient";
            yield return "config/host";
            yield return "config/importlist";
            yield return "config/indexer";
            yield return "config/mediamanagement";
            yield return "config/naming";
            yield return "config/ui";
        }

        private T GetV5<T>(string resource)
            where T : new()
        {
            return Read<T>(ApiV5.Get(resource));
        }

        private T GetV3<T>(string resource)
            where T : new()
        {
            return Read<T>(ApiV3.Get(resource));
        }

        private T PutV5<T>(string resource, T body, HttpStatusCode statusCode = HttpStatusCode.Accepted)
            where T : new()
        {
            return Read<T>(ApiV5.Put($"{resource}/1", body, statusCode));
        }

        private T PutV3<T>(string resource, T body, HttpStatusCode statusCode = HttpStatusCode.Accepted)
            where T : new()
        {
            return Read<T>(ApiV3.Put($"{resource}/1", body, statusCode));
        }

        private static T Copy<T>(T resource)
            where T : new()
        {
            return Json.Deserialize<T>(resource.ToJson());
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
            var propertyNames = errors
                .Select(error => error?["propertyName"]?.GetValue<string>())
                .Where(name => name != null);

            propertyNames.Should().Contain(name => string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase));

            return errors;
        }
    }
}
