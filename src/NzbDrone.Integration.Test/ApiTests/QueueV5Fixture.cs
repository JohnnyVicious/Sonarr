using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Queue;
using RestSharp;
using Sonarr.Http;
using V3QueueResource = Sonarr.Api.V3.Queue.QueueResource;
using V5QueueBulkResource = Sonarr.Api.V5.Queue.QueueBulkResource;
using V5QueueResource = Sonarr.Api.V5.Queue.QueueResource;
using V5QueueStatusResource = Sonarr.Api.V5.Queue.QueueStatusResource;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class QueueV5Fixture : IntegrationTest
    {
        [Test]
        public void should_declare_v5_queue_operations_in_openapi()
        {
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "queue", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "queue/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "queue/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.DELETE, "queue/bulk", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "queue/details", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.GET, "queue/status", HttpStatusCode.OK);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "queue/grab/{id}", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "queue/grab/{id}", HttpStatusCode.NotFound);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "queue/grab/bulk", HttpStatusCode.NoContent);
            ApiV5.OpenApi.ShouldDeclareResponse(Method.POST, "queue/grab/bulk", HttpStatusCode.NotFound);
        }

        [Test]
        public void should_reject_unauthenticated_v5_queue_requests()
        {
            ApiV5.Get("queue", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("queue/details", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Get("queue/status", HttpStatusCode.Unauthorized, authenticated: false);
            ApiV5.Delete("queue/1", HttpStatusCode.Unauthorized, authenticated: false);
            DeleteQueueBulk(new V5QueueBulkResource { Ids = new List<int> { 1 } }, HttpStatusCode.Unauthorized, authenticated: false);
            PostQueueGrab(1, HttpStatusCode.Unauthorized, authenticated: false);
            PostQueueGrabBulk(new V5QueueBulkResource { Ids = new List<int> { 1 } }, HttpStatusCode.Unauthorized, authenticated: false);
        }

        [Test]
        public void should_return_empty_v5_queue_status_and_details()
        {
            EnsureNoDownloadClient();

            var queue = GetQueuePage();
            var details = GetQueueDetails();
            var status = GetQueueStatus();

            queue.TotalRecords.Should().Be(0);
            queue.Records.Should().BeEmpty();
            details.Should().BeEmpty();
            status.TotalCount.Should().Be(0);
            status.Count.Should().Be(0);
            status.UnknownCount.Should().Be(0);
            status.Errors.Should().BeFalse();
            status.Warnings.Should().BeFalse();
            status.UnknownErrors.Should().BeFalse();
            status.UnknownWarnings.Should().BeFalse();
        }

        [Test]
        public void should_page_filter_detail_and_delete_v5_queue_item()
        {
            EnsureNoDownloadClient();

            var queued = TestData.QueuedDownload();
            var item = GetV5QueueItem(queued);

            item.SeriesId.Should().BeNull();
            item.DownloadClient.Should().Be(queued.DownloadClient);
            item.Status.Should().NotBe(QueueStatus.Unknown);

            var queue = GetQueuePage(includeUnknownSeriesItems: true, sortKey: "title", sortDirection: "ascending");
            queue.TotalRecords.Should().BeGreaterOrEqualTo(1);
            queue.Records.Should().Contain(record => record.Id == item.Id);

            var byStatus = GetQueuePage(includeUnknownSeriesItems: true, status: item.Status);
            byStatus.Records.Should().Contain(record => record.Id == item.Id);

            var knownOnly = GetQueuePage(includeUnknownSeriesItems: false);
            knownOnly.Records.Should().NotContain(record => record.Id == item.Id);

            GetQueueDetails().Should().Contain(record => record.Id == item.Id);

            var status = GetQueueStatus();
            status.TotalCount.Should().BeGreaterOrEqualTo(1);
            status.UnknownCount.Should().BeGreaterOrEqualTo(1);

            DeleteQueueItem(item.Id);
            WaitForQueueItemToDisappear(item.Id);
            ApiV5.Delete($"queue/{item.Id}", HttpStatusCode.NotFound);
        }

        [Test]
        public void should_bulk_delete_v5_queue_items()
        {
            EnsureNoDownloadClient();

            var first = GetV5QueueItem(TestData.QueuedDownload("Queue.Bulk.One.S01E01.mkv"));
            var second = GetV5QueueItem(TestData.QueuedDownload("Queue.Bulk.Two.S01E01.mkv"));
            var ids = new List<int> { first.Id, second.Id };

            DeleteQueueBulk(new V5QueueBulkResource { Ids = ids });

            ids.ForEach(WaitForQueueItemToDisappear);
            GetQueuePage().Records.Should().NotContain(record => ids.Contains(record.Id));
        }

        [Test]
        public void should_return_not_found_for_missing_v5_queue_actions()
        {
            ApiV5.Delete("queue/1000000", HttpStatusCode.NotFound);

            // Successful grab requires pending-release state from delay/fallback decisions; blackhole queue
            // fixtures produce tracked downloads, so this verifies grab routing without private DB setup.
            PostQueueGrab(1000000, HttpStatusCode.NotFound);
            PostQueueGrabBulk(new V5QueueBulkResource { Ids = new List<int> { 1000000 } }, HttpStatusCode.NotFound);
        }

        private PagingResource<V5QueueResource> GetQueuePage(bool includeUnknownSeriesItems = true, QueueStatus? status = null, string sortKey = "added", string sortDirection = "descending")
        {
            var request = ApiV5.BuildRequest("queue");
            request.AddParameter("page", 1);
            request.AddParameter("pageSize", 1000);
            request.AddParameter("sortKey", sortKey);
            request.AddParameter("sortDirection", sortDirection);
            request.AddParameter("includeUnknownSeriesItems", includeUnknownSeriesItems);

            if (status.HasValue)
            {
                request.AddParameter("status", status.Value);
            }

            return ApiV5.Execute<PagingResource<V5QueueResource>>(request);
        }

        private List<V5QueueResource> GetQueueDetails()
        {
            return ApiV5.Execute<List<V5QueueResource>>(ApiV5.BuildRequest("queue/details"));
        }

        private V5QueueStatusResource GetQueueStatus()
        {
            return ApiV5.Execute<V5QueueStatusResource>(ApiV5.BuildRequest("queue/status"));
        }

        private V5QueueResource GetV5QueueItem(V3QueueResource queued)
        {
            return GetQueuePage()
                .Records
                .Single(record => MatchesQueuedDownload(record, queued));
        }

        private void DeleteQueueItem(int id)
        {
            ApiV5.Delete($"queue/{id}", HttpStatusCode.NoContent);
        }

        private void DeleteQueueBulk(V5QueueBulkResource resource, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            var request = ApiV5.BuildRequest("queue/bulk", Method.DELETE);
            request.AddJsonBody(resource);

            ApiV5.Execute(request, statusCode, authenticated);
        }

        private void PostQueueGrab(int id, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            ApiV5.Execute(ApiV5.BuildRequest($"queue/grab/{id}", Method.POST), statusCode, authenticated);
        }

        private void PostQueueGrabBulk(V5QueueBulkResource resource, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool authenticated = true)
        {
            var request = ApiV5.BuildRequest("queue/grab/bulk", Method.POST);
            request.AddJsonBody(resource);

            ApiV5.Execute(request, statusCode, authenticated);
        }

        private void WaitForQueueItemToDisappear(int id)
        {
            WaitForCompletion(
                () => GetQueuePage().Records.All(record => record.Id != id),
                30000,
                1000);
        }

        private static bool MatchesQueuedDownload(V5QueueResource record, V3QueueResource queued)
        {
            return string.Equals(record.OutputPath, queued.OutputPath, System.StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(record.DownloadClient, queued.DownloadClient, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(record.Title, queued.Title, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
