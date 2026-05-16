using System;
using System.Net;
using NLog;
using NzbDrone.Common.Serializer;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public sealed class VersionedApiClient
    {
        private readonly Logger _logger;
        private readonly Lazy<OpenApiSpecification> _openApi;

        public VersionedApiClient(Uri rootUrl, string version, string apiKey)
        {
            Version = NormalizeVersion(version);
            ApiKey = apiKey;
            var apiRootUrl = ApiRootUrl(rootUrl, Version);
            var feedRootUrl = FeedRootUrl(rootUrl, Version);
            AuthenticatedRestClient = BuildRestClient(apiRootUrl, apiKey);

            // nosemgrep: csharp.lang.security.ssrf.rest-client.ssrf
            UnauthenticatedRestClient = new RestClient(apiRootUrl);
            AuthenticatedFeedRestClient = BuildRestClient(feedRootUrl, apiKey);

            // nosemgrep: csharp.lang.security.ssrf.rest-client.ssrf
            UnauthenticatedFeedRestClient = new RestClient(feedRootUrl);
            _logger = LogManager.GetLogger("REST");
            _openApi = new Lazy<OpenApiSpecification>(() => OpenApiSpecification.Load(Version));
        }

        public string Version { get; }
        public string ApiKey { get; }
        public RestClient AuthenticatedRestClient { get; }
        public RestClient UnauthenticatedRestClient { get; }
        public RestClient AuthenticatedFeedRestClient { get; }
        public RestClient UnauthenticatedFeedRestClient { get; }
        public OpenApiSpecification OpenApi => _openApi.Value;

        public RestRequest BuildRequest(string resource)
        {
            return BuildRequest(resource, Method.GET);
        }

        public RestRequest BuildRequest(string resource, Method method)
        {
            return new RestRequest(RequestResource(resource, Version))
            {
                Method = method,
                RequestFormat = DataFormat.Json
            };
        }

        public IRestResponse Get(string resource)
        {
            return Get(resource, HttpStatusCode.OK, true);
        }

        public IRestResponse Get(string resource, bool authenticated)
        {
            return Get(resource, HttpStatusCode.OK, authenticated);
        }

        public IRestResponse Get(string resource, HttpStatusCode statusCode)
        {
            return Get(resource, statusCode, true);
        }

        public IRestResponse Get(string resource, HttpStatusCode statusCode, bool authenticated)
        {
            return Execute(BuildRequest(resource), statusCode, authenticated);
        }

        public IRestResponse Post(string resource, object body)
        {
            return Post(resource, body, HttpStatusCode.Created, true);
        }

        public IRestResponse Post(string resource, object body, bool authenticated)
        {
            return Post(resource, body, HttpStatusCode.Created, authenticated);
        }

        public IRestResponse Post(string resource, object body, HttpStatusCode statusCode)
        {
            return Post(resource, body, statusCode, true);
        }

        public IRestResponse Post(string resource, object body, HttpStatusCode statusCode, bool authenticated)
        {
            var request = BuildRequest(resource, Method.POST);
            request.AddJsonBody(body);

            return Execute(request, statusCode, authenticated);
        }

        public IRestResponse Put(string resource, object body)
        {
            return Put(resource, body, HttpStatusCode.Accepted, true);
        }

        public IRestResponse Put(string resource, object body, bool authenticated)
        {
            return Put(resource, body, HttpStatusCode.Accepted, authenticated);
        }

        public IRestResponse Put(string resource, object body, HttpStatusCode statusCode)
        {
            return Put(resource, body, statusCode, true);
        }

        public IRestResponse Put(string resource, object body, HttpStatusCode statusCode, bool authenticated)
        {
            var request = BuildRequest(resource, Method.PUT);
            request.AddJsonBody(body);

            return Execute(request, statusCode, authenticated);
        }

        public IRestResponse Delete(string resource)
        {
            return Delete(resource, HttpStatusCode.OK, true);
        }

        public IRestResponse Delete(string resource, bool authenticated)
        {
            return Delete(resource, HttpStatusCode.OK, authenticated);
        }

        public IRestResponse Delete(string resource, HttpStatusCode statusCode)
        {
            return Delete(resource, statusCode, true);
        }

        public IRestResponse Delete(string resource, HttpStatusCode statusCode, bool authenticated)
        {
            return Execute(BuildRequest(resource, Method.DELETE), statusCode, authenticated);
        }

        public T Execute<T>(RestRequest request)
            where T : new()
        {
            return Execute<T>(request, HttpStatusCode.OK, true);
        }

        public T Execute<T>(RestRequest request, bool authenticated)
            where T : new()
        {
            return Execute<T>(request, HttpStatusCode.OK, authenticated);
        }

        public T Execute<T>(RestRequest request, HttpStatusCode statusCode)
            where T : new()
        {
            return Execute<T>(request, statusCode, true);
        }

        public T Execute<T>(RestRequest request, HttpStatusCode statusCode, bool authenticated)
            where T : new()
        {
            var response = Execute(request, statusCode, authenticated);

            return Json.Deserialize<T>(response.Content);
        }

        public IRestResponse Execute(RestRequest request)
        {
            return Execute(request, HttpStatusCode.OK, true);
        }

        public IRestResponse Execute(RestRequest request, bool authenticated)
        {
            return Execute(request, HttpStatusCode.OK, authenticated);
        }

        public IRestResponse Execute(RestRequest request, HttpStatusCode statusCode)
        {
            return Execute(request, statusCode, true);
        }

        public IRestResponse Execute(RestRequest request, HttpStatusCode statusCode, bool authenticated)
        {
            return WithRoutedRequest(request, authenticated, client =>
            {
                _logger.Info("{0}: {1}", request.Method, client.BuildUri(request));

                var response = client.Execute(request);

                _logger.Info("Response: {0}", response.Content);

                return response.ShouldHaveStatusCode(statusCode).ShouldDisableCache();
            });
        }

        public Uri BuildUri(RestRequest request)
        {
            return BuildUri(request, true);
        }

        public Uri BuildUri(RestRequest request, bool authenticated)
        {
            return WithRoutedRequest(request, authenticated, client => client.BuildUri(request));
        }

        public string ApiPath(string resource)
        {
            return OpenApi.ApiPath(resource);
        }

        private static RestClient BuildRestClient(Uri apiRootUrl, string apiKey)
        {
            // nosemgrep: csharp.lang.security.ssrf.rest-client.ssrf
            var restClient = new RestClient(apiRootUrl);
            restClient.AddDefaultHeader("Authorization", apiKey);
            restClient.AddDefaultHeader("X-Api-Key", apiKey);

            return restClient;
        }

        private static Uri ApiRootUrl(Uri rootUrl, string version)
        {
            ValidateRootUrl(rootUrl);

            var builder = new UriBuilder(rootUrl)
            {
                Path = $"api/{version}/",
                Query = string.Empty,
                Fragment = string.Empty
            };

            return builder.Uri;
        }

        private static Uri FeedRootUrl(Uri rootUrl, string version)
        {
            ValidateRootUrl(rootUrl);

            var builder = new UriBuilder(rootUrl)
            {
                Path = $"feed/{version}/",
                Query = string.Empty,
                Fragment = string.Empty
            };

            return builder.Uri;
        }

        private static void ValidateRootUrl(Uri rootUrl)
        {
            if (rootUrl.Scheme != Uri.UriSchemeHttp && rootUrl.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("API test clients only support HTTP(S) Sonarr roots.", nameof(rootUrl));
            }

            if (!rootUrl.IsLoopback)
            {
                throw new ArgumentException("API test clients must target a loopback Sonarr instance.", nameof(rootUrl));
            }
        }

        private RestClient SelectRestClient(RestRequest request, bool authenticated)
        {
            if (request.Resource.StartsWith("feed/", StringComparison.Ordinal))
            {
                return authenticated ? AuthenticatedFeedRestClient : UnauthenticatedFeedRestClient;
            }

            return authenticated ? AuthenticatedRestClient : UnauthenticatedRestClient;
        }

        private T WithRoutedRequest<T>(RestRequest request, bool authenticated, Func<RestClient, T> action)
        {
            var client = SelectRestClient(request, authenticated);
            var originalResource = request.Resource;

            if (originalResource.StartsWith("feed/", StringComparison.Ordinal))
            {
                request.Resource = originalResource["feed/".Length..];
            }

            try
            {
                return action(client);
            }
            finally
            {
                request.Resource = originalResource;
            }
        }

        private static string RequestResource(string resource, string version)
        {
            var normalizedResource = resource.TrimStart('/');
            var versionedApiPrefix = $"api/{version}/";
            var versionedFeedPrefix = $"feed/{version}/";

            if (normalizedResource.StartsWith(versionedApiPrefix, StringComparison.Ordinal))
            {
                return normalizedResource[versionedApiPrefix.Length..];
            }

            if (normalizedResource.StartsWith(versionedFeedPrefix, StringComparison.Ordinal))
            {
                return $"feed/{normalizedResource[versionedFeedPrefix.Length..]}";
            }

            return normalizedResource;
        }

        private static string NormalizeVersion(string version)
        {
            return version.StartsWith("v", StringComparison.Ordinal) ? version : $"v{version}";
        }
    }
}
