using System.Collections.Generic; // NOSONAR
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Test.Common;
using Sonarr.Http;

namespace NzbDrone.Http.Test
{
    [TestFixture]
    public class PagingResourceFixture : TestBase
    {
        [Test]
        public void should_default_page_to_one_when_request_page_is_null()
        {
            var request = new PagingRequestResource { Page = null, PageSize = 10 };
            var resource = new PagingResource<string>(request);

            resource.Page.Should().Be(1);
        }

        [Test]
        public void should_default_page_size_to_ten_when_request_is_null()
        {
            var request = new PagingRequestResource { Page = 1, PageSize = null };
            var resource = new PagingResource<string>(request);

            resource.PageSize.Should().Be(10);
        }

        [Test]
        public void should_default_sort_direction_to_descending_when_null()
        {
            var request = new PagingRequestResource { Page = 1, PageSize = 10, SortDirection = null };
            var resource = new PagingResource<string>(request);

            resource.SortDirection.Should().Be(SortDirection.Descending);
        }

        [Test]
        public void should_use_provided_page_and_page_size()
        {
            var request = new PagingRequestResource { Page = 3, PageSize = 25 };
            var resource = new PagingResource<string>(request);

            resource.Page.Should().Be(3);
            resource.PageSize.Should().Be(25);
        }

        [Test]
        public void should_use_provided_sort_key()
        {
            var request = new PagingRequestResource { Page = 1, PageSize = 10, SortKey = "title" };
            var resource = new PagingResource<string>(request);

            resource.SortKey.Should().Be("title");
        }

        [Test]
        public void should_use_provided_sort_direction()
        {
            var request = new PagingRequestResource { Page = 1, PageSize = 10, SortDirection = SortDirection.Ascending };
            var resource = new PagingResource<string>(request);

            resource.SortDirection.Should().Be(SortDirection.Ascending);
        }

        [Test]
        public void should_have_empty_records_list_by_default()
        {
            var resource = new PagingResource<string>();

            resource.Records.Should().NotBeNull();
            resource.Records.Should().BeEmpty();
        }

        [Test]
        public void map_to_paging_spec_should_use_allowed_sort_key()
        {
            var allowedKeys = new HashSet<string> { "title", "date" };
            var resource = new PagingResource<string> { Page = 1, PageSize = 10, SortKey = "title", SortDirection = SortDirection.Ascending };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys);

            spec.SortKey.Should().Be("title");
        }

        [Test]
        public void map_to_paging_spec_should_fall_back_to_default_for_invalid_sort_key()
        {
            var allowedKeys = new HashSet<string> { "title", "date" };
            var resource = new PagingResource<string> { Page = 1, PageSize = 10, SortKey = "invalid", SortDirection = SortDirection.Ascending };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys, "id");

            spec.SortKey.Should().Be("id");
        }

        [Test]
        public void map_to_paging_spec_should_fall_back_to_default_for_null_sort_key()
        {
            var allowedKeys = new HashSet<string> { "title", "date" };
            var resource = new PagingResource<string> { Page = 1, PageSize = 10, SortKey = null, SortDirection = SortDirection.Ascending };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys, "id");

            spec.SortKey.Should().Be("id");
        }

        [Test]
        public void map_to_paging_spec_should_use_default_direction_when_sort_direction_is_default()
        {
            var allowedKeys = new HashSet<string> { "title" };
            var resource = new PagingResource<string> { Page = 1, PageSize = 10, SortKey = "title", SortDirection = SortDirection.Default };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys, "id", SortDirection.Descending);

            spec.SortDirection.Should().Be(SortDirection.Descending);
        }

        [Test]
        public void map_to_paging_spec_should_preserve_page_and_page_size()
        {
            var allowedKeys = new HashSet<string> { "title" };
            var resource = new PagingResource<string> { Page = 5, PageSize = 50, SortKey = "title", SortDirection = SortDirection.Ascending };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys);

            spec.Page.Should().Be(5);
            spec.PageSize.Should().Be(50);
        }

        [Test]
        public void map_to_paging_spec_should_fall_back_to_default_when_allowed_keys_empty()
        {
            var allowedKeys = new HashSet<string>();
            var resource = new PagingResource<string> { Page = 1, PageSize = 10, SortKey = "title", SortDirection = SortDirection.Ascending };

            var spec = resource.MapToPagingSpec<string, object>(allowedKeys, "id");

            spec.SortKey.Should().Be("id");
        }
    }
}
