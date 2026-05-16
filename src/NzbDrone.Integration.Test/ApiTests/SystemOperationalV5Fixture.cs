using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Integration.Test.Client;
using RestSharp;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class SystemOperationalV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_system_operational_routes_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/status", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/task", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/task/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/task/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/routes", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/routes/duplicate", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "system/backup", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "system/backup/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "system/backup/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/backup/restore/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/backup/restore/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/backup/restore/upload", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/backup/restore/upload", HttpStatusCode.BadRequest);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/restart", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "system/shutdown", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "health", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "command", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "command", HttpStatusCode.Created);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "command/{id}", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "command/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "command/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "diskspace", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "update", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "log", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "log/file", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareOperation(Method.GET, "log/file/{filename}");
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "log/file/update", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareOperation(Method.GET, "log/file/update/{filename}");
        }

        [Test]
        public void should_reject_unauthenticated_system_operational_requests()
        {
            ExecuteUnauthenticated("system/status", Method.GET);
            ExecuteUnauthenticated("system/task", Method.GET);
            ExecuteUnauthenticated("system/task/1", Method.GET);
            ExecuteUnauthenticated("system/routes", Method.GET);
            ExecuteUnauthenticated("system/routes/duplicate", Method.GET);
            ExecuteUnauthenticated("system/backup", Method.GET);
            ExecuteUnauthenticated("system/backup/1", Method.DELETE);
            ExecuteUnauthenticated("system/backup/restore/1", Method.POST, new object());
            ExecuteUnauthenticated("system/backup/restore/upload", Method.POST, new object());

            // Authenticated restart and shutdown intentionally are not invoked in integration tests
            // because they terminate the test host. OpenAPI plus auth rejection covers routing.
            ExecuteUnauthenticated("system/restart", Method.POST, new object());
            ExecuteUnauthenticated("system/shutdown", Method.POST, new object());

            ExecuteUnauthenticated("health", Method.GET);
            ExecuteUnauthenticated("command", Method.GET);
            ExecuteUnauthenticated("command", Method.POST, new { name = "Backup" });
            ExecuteUnauthenticated("command/1", Method.GET);
            ExecuteUnauthenticated("command/1", Method.DELETE);
            ExecuteUnauthenticated("diskspace", Method.GET);
            ExecuteUnauthenticated("update", Method.GET);
            ExecuteUnauthenticated("log", Method.GET);
            ExecuteUnauthenticated("log/file", Method.GET);
            ExecuteUnauthenticated("log/file/sonarr.txt", Method.GET);
            ExecuteUnauthenticated("log/file/update", Method.GET);
            ExecuteUnauthenticated("log/file/update/sonarr.txt", Method.GET);
        }

        [Test]
        public void should_read_system_status_tasks_routes_health_disk_update_and_logs()
        {
            var status = ReadObject(ApiV5.Get("system/status"));
            status["appName"]!.GetValue<string>().Should().Be("Sonarr");
            status["version"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            status["appData"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            status["isNetCore"]!.GetValue<bool>().Should().BeTrue();

            var tasks = ReadArray(ApiV5.Get("system/task"));
            tasks.Should().NotBeEmpty();
            var taskId = tasks.First()!["id"]!.GetValue<int>();
            var task = ReadObject(ApiV5.Get($"system/task/{taskId}"));
            task["id"]!.GetValue<int>().Should().Be(taskId);
            task["taskName"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

            ApiV5.Get("system/task/1000000", HttpStatusCode.NotFound);

            var routes = ApiV5.Get("system/routes");
            routes.ContentType.Should().StartWith("text/plain");
            routes.Content.Should().Contain("/api/v5/system/status");

            var duplicateRoutes = ReadObject(ApiV5.Get("system/routes/duplicate"));
            duplicateRoutes.Should().NotBeNull();

            ReadArray(ApiV5.Get("health")).Should().NotBeNull();
            ReadArray(ApiV5.Get("diskspace")).Should().NotBeNull();
            ReadArray(ApiV5.Get("update")).Should().NotBeNull();

            var logs = ReadObject(ApiV5.Get("log?page=1&pageSize=5&sortKey=time&sortDirection=descending&level=trace"));
            logs["page"]!.GetValue<int>().Should().Be(1);
            logs["pageSize"]!.GetValue<int>().Should().Be(5);
            logs["records"].Should().NotBeNull();
        }

        [Test]
        public void should_create_read_and_reject_unsafe_command_deletes()
        {
            try
            {
                var created = ReadObject(ApiV5.Post("command", new { name = "Backup" }, HttpStatusCode.Created));
                var commandId = created["id"]!.GetValue<int>();
                commandId.Should().BeGreaterThan(0);
                created["name"]!.GetValue<string>().Should().Be("Backup");

                var completed = WaitForCommand(commandId);
                CommandStatus(completed).Should().Be("completed");

                var commands = ReadArray(ApiV5.Get("command"));
                commands.Any(command => command?["id"]?.GetValue<int>() == commandId).Should().BeTrue();
                ApiV5.Get($"command/{commandId}", HttpStatusCode.OK).ShouldHaveJsonObjectContent();

                ApiV5.Delete($"command/{commandId}", HttpStatusCode.Conflict);
                ApiV5.Get("command/1000000", HttpStatusCode.NotFound);
            }
            finally
            {
                DeleteAllBackups();
            }
        }

        [Test]
        public void should_list_create_and_delete_manual_backups()
        {
            var backupId = 0;

            DeleteAllBackups();

            try
            {
                ReadArray(ApiV5.Get("system/backup")).Should().BeEmpty();

                var command = ReadObject(ApiV5.Post("command", new { name = "Backup" }, HttpStatusCode.Created));
                WaitForCommand(command["id"]!.GetValue<int>());

                var backups = ReadArray(ApiV5.Get("system/backup"));
                backups.Should().NotBeEmpty();

                var backup = backups.First(item => item?["id"]?.GetValue<int>() > 0);
                backupId = backup!["id"]!.GetValue<int>();

                backup["name"]!.GetValue<string>().Should().EndWith(".zip");
                backup["path"]!.GetValue<string>().Should().Contain("/backup/");
                backup["size"]!.GetValue<long>().Should().BeGreaterThan(0);

                ApiV5.Delete($"system/backup/{backupId}", HttpStatusCode.NoContent);
                backupId = 0;
                ApiV5.Delete("system/backup/1000000", HttpStatusCode.NotFound);
                ApiV5.Post("system/backup/restore/1000000", new object(), HttpStatusCode.NotFound);
            }
            finally
            {
                if (backupId > 0)
                {
                    ApiV5.Delete($"system/backup/{backupId}", HttpStatusCode.NoContent);
                }

                DeleteAllBackups();
            }
        }

        [Test]
        public void should_validate_backup_restore_upload_inputs()
        {
            var emptyUpload = ApiV5.BuildRequest("system/backup/restore/upload", Method.POST);
            emptyUpload.AlwaysMultipartFormData = true;
            ApiV5.Execute(emptyUpload, HttpStatusCode.BadRequest);

            var invalidUpload = ApiV5.BuildRequest("system/backup/restore/upload", Method.POST);
            invalidUpload.AddFile("file", Encoding.UTF8.GetBytes("not a backup"), "not-a-backup.txt", "text/plain");
            var response = ApiV5.Execute(invalidUpload, HttpStatusCode.BadRequest);
            response.ShouldHaveJsonObjectContent()["error"]!.GetValue<string>().Should().Contain("Invalid extension");
        }

        [Test]
        public void should_list_and_serve_log_files_with_safe_paths()
        {
            var appData = ReadObject(ApiV5.Get("system/status"))["appData"]!.GetValue<string>();
            var logFileName = TestData.NextName("sonarr-api-test") + ".txt";
            var updateLogFileName = TestData.NextName("sonarr-update-api-test") + ".txt";
            var logFile = WriteTextFile(Path.Combine(appData, "logs"), logFileName, "sonarr log fixture");
            var updateLogFile = WriteTextFile(Path.Combine(appData, "UpdateLogs"), updateLogFileName, "sonarr update log fixture");

            try
            {
                var logFiles = ReadArray(ApiV5.Get("log/file"));
                logFiles.Any(item => item?["filename"]?.GetValue<string>() == Path.GetFileName(logFile)).Should().BeTrue();

                var logResponse = ApiV5.Get($"log/file/{logFileName}");
                logResponse.ContentType.Should().StartWith("text/plain");
                logResponse.Content.Should().Contain("sonarr log fixture");
                ApiV5.Get("log/file/not-a-log.txt", HttpStatusCode.NotFound);
                ApiV5.Get("log/file/%2e%2e%2fconfig.txt", HttpStatusCode.NotFound);

                var updateLogFiles = ReadArray(ApiV5.Get("log/file/update"));
                updateLogFiles.Any(item => item?["filename"]?.GetValue<string>() == Path.GetFileName(updateLogFile)).Should().BeTrue();

                var updateLogResponse = ApiV5.Get($"log/file/update/{updateLogFileName}");
                updateLogResponse.ContentType.Should().StartWith("text/plain");
                updateLogResponse.Content.Should().Contain("sonarr update log fixture");
                ApiV5.Get("log/file/update/not-a-log.txt", HttpStatusCode.NotFound);
                ApiV5.Get("log/file/update/%2e%2e%2fconfig.txt", HttpStatusCode.NotFound);
            }
            finally
            {
                File.Delete(logFile);
                File.Delete(updateLogFile);
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

        private static string CommandStatus(JsonObject command)
        {
            return command["status"]!.GetValue<string>().ToLowerInvariant();
        }

        private static string WriteTextFile(string folder, string filename, string content)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, filename);
            File.WriteAllText(path, content);

            return path;
        }

        private JsonObject WaitForCommand(int commandId)
        {
            JsonObject command = null;

            WaitForCompletion(() =>
            {
                command = ReadObject(ApiV5.Get($"command/{commandId}"));
                var status = CommandStatus(command);

                if (status is "failed" or "aborted" or "cancelled" or "orphaned")
                {
                    throw new InvalidOperationException($"Command {commandId} ended with {status}.");
                }

                return status == "completed";
            },
            30000,
            500);

            return command;
        }

        private void DeleteAllBackups()
        {
            foreach (var backup in ReadArray(ApiV5.Get("system/backup")))
            {
                ApiV5.Delete($"system/backup/{backup!["id"]!.GetValue<int>()}", HttpStatusCode.NoContent);
            }
        }

        private void ExecuteUnauthenticated(string resource, Method method, object body = null)
        {
            var request = ApiV5.BuildRequest(resource, method);

            if (body != null)
            {
                request.AddJsonBody(body);
            }

            ApiV5.UnauthenticatedRestClient.Execute(request).ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
        }
    }
}
