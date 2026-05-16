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

        public VersionedApiClient(string rootUrl, string version, string apiKey)
        {
            Version = NormalizeVersion(version);
            ApiKey = apiKey;
            AuthenticatedRestClient = BuildRestClient(rootUrl, Version, apiKey);
            UnauthenticatedRestClient = new RestClient(ApiRootUrl(rootUrl, Version));
            _logger = LogManager.GetLogger("REST");
            _openApi = new Lazy<OpenApiSpecification>(() => OpenApiSpecification.Load(Version));
        }

        public string Version { get; }
        public string ApiKey { get; }
        public RestClient AuthenticatedRestClient { get; }
        public RestClient UnauthenticatedRestClient { get; }
        public OpenApiSpecification OpenApi => _openApi.Value;

        public RestRequest BuildRequest(string resource, Method method = Method.GET)
        {
            return new RestRequest(resource.TrimStart('/'))
            {
                Method = method,
                RequestFormat = DataFormat.Json
            };
        }

        public IRestResponse Get(string resource, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            return Execute(BuildRequest(resource), statusCode, authenticated);
        }

        public IRestResponse Post(string resource, object body, HttpStatusCode statusCode = HttpStatusCode.Created, bool authenticated = true)
        {
            var request = BuildRequest(resource, Method.POST);
            request.AddJsonBody(body);

            return Execute(request, statusCode, authenticated);
        }

        public IRestResponse Put(string resource, object body, HttpStatusCode statusCode = HttpStatusCode.Accepted, bool authenticated = true)
        {
            var request = BuildRequest(resource, Method.PUT);
            request.AddJsonBody(body);

            return Execute(request, statusCode, authenticated);
        }

        public IRestResponse Delete(string resource, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            return Execute(BuildRequest(resource, Method.DELETE), statusCode, authenticated);
        }

        public T Execute<T>(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
            where T : new()
        {
            var response = Execute(request, statusCode, authenticated);

            return Json.Deserialize<T>(response.Content);
        }

        public IRestResponse Execute(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK, bool authenticated = true)
        {
            var client = authenticated ? AuthenticatedRestClient : UnauthenticatedRestClient;

            _logger.Info("{0}: {1}", request.Method, client.BuildUri(request));

            var response = client.Execute(request);

            _logger.Info("Response: {0}", response.Content);

            return response.ShouldHaveStatusCode(statusCode);
        }

        public string ApiPath(string resource)
        {
            return OpenApi.ApiPath(resource);
        }

        private static RestClient BuildRestClient(string rootUrl, string version, string apiKey)
        {
            var restClient = new RestClient(ApiRootUrl(rootUrl, version));
            restClient.AddDefaultHeader("Authentication", apiKey);
            restClient.AddDefaultHeader("Authorization", apiKey);
            restClient.AddDefaultHeader("X-Api-Key", apiKey);

            return restClient;
        }

        private static string ApiRootUrl(string rootUrl, string version)
        {
            return $"{rootUrl.TrimEnd('/')}/api/{version}/";
        }

        private static string NormalizeVersion(string version)
        {
            return version.StartsWith("v", StringComparison.Ordinal) ? version : $"v{version}";
        }
    }
}
