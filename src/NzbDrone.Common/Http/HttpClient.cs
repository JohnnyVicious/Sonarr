using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;

namespace NzbDrone.Common.Http
{
    public interface IHttpClient
    {
        HttpResponse Execute(HttpRequest request);
        void DownloadFile(string url, string fileName);
        HttpResponse Get(HttpRequest request);
        HttpResponse<T> Get<T>(HttpRequest request)
            where T : new();
        HttpResponse Head(HttpRequest request);
        HttpResponse Post(HttpRequest request);
        HttpResponse<T> Post<T>(HttpRequest request)
            where T : new();

        Task<HttpResponse> ExecuteAsync(HttpRequest request);
        Task<HttpResponse> ExecuteAsync(HttpRequest request, CancellationToken cancellationToken);
        Task DownloadFileAsync(string source, string fileName);
        Task DownloadFileAsync(string source, string fileName, CancellationToken cancellationToken);
        Task DownloadFileAsync(Uri url, string fileName);
        Task DownloadFileAsync(Uri url, string fileName, CancellationToken cancellationToken);
        Task<HttpResponse> GetAsync(HttpRequest request);
        Task<HttpResponse> GetAsync(HttpRequest request, CancellationToken cancellationToken);
        Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new();
        Task<HttpResponse<T>> GetAsync<T>(HttpRequest request, CancellationToken cancellationToken)
            where T : new();
        Task<HttpResponse> HeadAsync(HttpRequest request);
        Task<HttpResponse> HeadAsync(HttpRequest request, CancellationToken cancellationToken);
        Task<HttpResponse> PostAsync(HttpRequest request);
        Task<HttpResponse> PostAsync(HttpRequest request, CancellationToken cancellationToken);
        Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new();
        Task<HttpResponse<T>> PostAsync<T>(HttpRequest request, CancellationToken cancellationToken)
            where T : new();
    }

    public class HttpClient : IHttpClient
    {
        private const int MaxRedirects = 5;

        private readonly Logger _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICached<CookieContainer> _cookieContainerCache;
        private readonly List<IHttpRequestInterceptor> _requestInterceptors;
        private readonly IHttpDispatcher _httpDispatcher;

        public HttpClient(IEnumerable<IHttpRequestInterceptor> requestInterceptors,
            ICacheManager cacheManager,
            IRateLimitService rateLimitService,
            IHttpDispatcher httpDispatcher,
            Logger logger)
        {
            _requestInterceptors = requestInterceptors.ToList();
            _rateLimitService = rateLimitService;
            _httpDispatcher = httpDispatcher;
            _logger = logger;

            _cookieContainerCache = cacheManager.GetCache<CookieContainer>(typeof(HttpClient));
        }

        public virtual Task<HttpResponse> ExecuteAsync(HttpRequest request)
        {
            return ExecuteAsync(request, CancellationToken.None);
        }

        public virtual async Task<HttpResponse> ExecuteAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            var cookieContainer = InitializeRequestCookies(request);
            var response = await ExecuteWithRedirectsAsync(request, cookieContainer, cancellationToken);

            LogDeveloperRedirect(response);
            ThrowIfHttpError(request, response);

            return response;
        }

        private async Task<HttpResponse> ExecuteWithRedirectsAsync(HttpRequest request, CookieContainer cookieContainer, CancellationToken cancellationToken)
        {
            var response = await ExecuteRequestAsync(request, cookieContainer, cancellationToken);

            if (!request.AllowAutoRedirect || !response.HasHttpRedirect)
            {
                return response;
            }

            var autoRedirectChain = new List<string> { request.Url.ToString() };

            do
            {
                response = await ExecuteRedirectAsync(request, cookieContainer, response, autoRedirectChain, cancellationToken);
            }
            while (response.HasHttpRedirect);

            return response;
        }

        public HttpResponse Execute(HttpRequest request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        private async Task<HttpResponse> ExecuteRedirectAsync(HttpRequest request,
                                                              CookieContainer cookieContainer,
                                                              HttpResponse response,
                                                              List<string> autoRedirectChain,
                                                              CancellationToken cancellationToken)
        {
            request.Url += new HttpUri(response.Headers.GetSingleValue("Location"));
            autoRedirectChain.Add(request.Url.ToString());

            _logger.Trace("Redirected to {0}", request.Url);

            if (autoRedirectChain.Count > MaxRedirects)
            {
                throw new WebException($"Too many automatic redirections were attempted for {autoRedirectChain.Join(" -> ")}", WebExceptionStatus.ProtocolError);
            }

            // 302 or 303 should default to GET on redirect even if POST on original
            if (RequestRequiresForceGet(response.StatusCode, response.Request.Method))
            {
                request.Method = HttpMethod.Get;
                request.ContentData = null;
                request.ContentSummary = null;
            }

            return await ExecuteRequestAsync(request, cookieContainer, cancellationToken);
        }

        private void LogDeveloperRedirect(HttpResponse response)
        {
            if (response.HasHttpRedirect && !RuntimeInfo.IsProduction)
            {
                _logger.Error("Server requested a redirect to [{0}] while in developer mode. Update the request URL to avoid this redirect.", response.Headers["Location"]);
            }
        }

        private void ThrowIfHttpError(HttpRequest request, HttpResponse response)
        {
            if (request.SuppressHttpError || !response.HasHttpError || request.SuppressHttpErrorStatusCodes?.Contains(response.StatusCode) == true)
            {
                return;
            }

            if (request.LogHttpError)
            {
                _logger.Warn("HTTP Error - {0}", response);
            }

            if ((int)response.StatusCode == 429)
            {
                throw new TooManyRequestsException(request, response);
            }

            throw new HttpException(request, response);
        }

        private static bool RequestRequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod)
        {
            return statusCode switch
            {
                HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.MultipleChoices => requestMethod == HttpMethod.Post,
                HttpStatusCode.SeeOther => requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head,
                _ => false,
            };
        }

        private async Task<HttpResponse> ExecuteRequestAsync(HttpRequest request, CookieContainer cookieContainer, CancellationToken cancellationToken)
        {
            foreach (var interceptor in _requestInterceptors)
            {
                request = interceptor.PreRequest(request);
            }

            if (request.RateLimit != TimeSpan.Zero)
            {
                await _rateLimitService.WaitAndPulseAsync(request.Url.Host, request.RateLimitKey, request.RateLimit);
            }

            _logger.Trace(request);

            var stopWatch = Stopwatch.StartNew();

            var response = await _httpDispatcher.GetResponseAsync(request, cookieContainer, cancellationToken);

            HandleResponseCookies(response, cookieContainer);

            stopWatch.Stop();

            _logger.Trace("{0} ({1} ms)", response, stopWatch.ElapsedMilliseconds);

            foreach (var interceptor in _requestInterceptors)
            {
                response = interceptor.PostResponse(response);
            }

            if (request.LogResponseContent && response.ResponseData != null)
            {
                _logger.Trace("Response content ({0} bytes): {1}", response.ResponseData.Length, response.Content);
            }

            return response;
        }

        private CookieContainer InitializeRequestCookies(HttpRequest request)
        {
            lock (_cookieContainerCache)
            {
                var sourceContainer = new CookieContainer();

                var presistentContainer = _cookieContainerCache.Get("container", () => new CookieContainer());
                var persistentCookies = presistentContainer.GetCookies((Uri)request.Url);
                sourceContainer.Add(persistentCookies);

                if (request.Cookies.Count != 0)
                {
                    foreach (var pair in request.Cookies)
                    {
                        Cookie cookie;
                        if (pair.Value == null)
                        {
                            cookie = new Cookie(pair.Key, "", "/")
                            {
                                Expires = DateTime.Now.AddDays(-1)
                            };
                        }
                        else
                        {
                            cookie = new Cookie(pair.Key, pair.Value, "/")
                            {
                                // Use Now rather than UtcNow to work around Mono cookie expiry bug.
                                // See https://gist.github.com/ta264/7822b1424f72e5b4c961
                                Expires = DateTime.Now.AddHours(1)
                            };
                        }

                        sourceContainer.Add((Uri)request.Url, cookie);

                        if (request.StoreRequestCookie)
                        {
                            presistentContainer.Add((Uri)request.Url, cookie);
                        }
                    }
                }

                return sourceContainer;
            }
        }

        private void HandleResponseCookies(HttpResponse response, CookieContainer container)
        {
            foreach (Cookie cookie in container.GetAllCookies())
            {
                cookie.Expired = true;
            }

            var cookieHeaders = response.GetCookieHeaders();

            if (cookieHeaders.Empty())
            {
                return;
            }

            AddCookiesToContainer(response.Request.Url, cookieHeaders, container);

            if (response.Request.StoreResponseCookie)
            {
                lock (_cookieContainerCache)
                {
                    var persistentCookieContainer = _cookieContainerCache.Get("container", () => new CookieContainer());

                    AddCookiesToContainer(response.Request.Url, cookieHeaders, persistentCookieContainer);
                }
            }
        }

        private void AddCookiesToContainer(HttpUri url, string[] cookieHeaders, CookieContainer container)
        {
            foreach (var cookieHeader in cookieHeaders)
            {
                try
                {
                    container.SetCookies((Uri)url, cookieHeader);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Invalid cookie in {0}", url);
                }
            }
        }

        public Task DownloadFileAsync(string source, string fileName)
        {
            return DownloadFileAsync(source, fileName, CancellationToken.None);
        }

        public Task DownloadFileAsync(string source, string fileName, CancellationToken cancellationToken)
        {
            return DownloadFileAsync(new HttpRequest(source), source, fileName, cancellationToken);
        }

        public Task DownloadFileAsync(Uri url, string fileName)
        {
            return DownloadFileAsync(url, fileName, CancellationToken.None);
        }

        public async Task DownloadFileAsync(Uri url, string fileName, CancellationToken cancellationToken)
        {
            await DownloadFileAsync(new HttpRequest(url.OriginalString), url.OriginalString, fileName, cancellationToken);
        }

        private async Task DownloadFileAsync(HttpRequest request, string source, string fileName, CancellationToken cancellationToken)
        {
            var fileNamePart = fileName + ".part";

            try
            {
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                _logger.Debug("Downloading [{0}] to [{1}]", source, fileName);

                var stopWatch = Stopwatch.StartNew();
                await using (var fileStream = new FileStream(fileNamePart, FileMode.Create, FileAccess.ReadWrite))
                {
                    request.AllowAutoRedirect = true;
                    request.ResponseStream = fileStream;
                    request.RequestTimeout = TimeSpan.FromSeconds(300);
                    var response = await GetAsync(request, cancellationToken);

                    if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
                    {
                        throw new HttpException(request, response, "Site responded with html content.");
                    }
                }

                stopWatch.Stop();

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                File.Move(fileNamePart, fileName);
                _logger.Debug("Downloading Completed. took {0:0}s", stopWatch.Elapsed.Seconds);
            }
            finally
            {
                if (File.Exists(fileNamePart))
                {
                    File.Delete(fileNamePart);
                }
            }
        }

        public void DownloadFile(string url, string fileName)
        {
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development#the-thread-pool-hack
            Task.Run(() => DownloadFileAsync(url, fileName)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> GetAsync(HttpRequest request)
        {
            return GetAsync(request, CancellationToken.None);
        }

        public Task<HttpResponse> GetAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            request.Method = HttpMethod.Get;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Get(HttpRequest request)
        {
            return Task.Run(() => GetAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new()
        {
            return GetAsync<T>(request, CancellationToken.None);
        }

        public async Task<HttpResponse<T>> GetAsync<T>(HttpRequest request, CancellationToken cancellationToken)
            where T : new()
        {
            var response = await GetAsync(request, cancellationToken);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Get<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => GetAsync<T>(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> HeadAsync(HttpRequest request)
        {
            return HeadAsync(request, CancellationToken.None);
        }

        public Task<HttpResponse> HeadAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            request.Method = HttpMethod.Head;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Head(HttpRequest request)
        {
            return Task.Run(() => HeadAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> PostAsync(HttpRequest request)
        {
            return PostAsync(request, CancellationToken.None);
        }

        public Task<HttpResponse> PostAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            request.Method = HttpMethod.Post;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Post(HttpRequest request)
        {
            return Task.Run(() => PostAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new()
        {
            return PostAsync<T>(request, CancellationToken.None);
        }

        public async Task<HttpResponse<T>> PostAsync<T>(HttpRequest request, CancellationToken cancellationToken)
            where T : new()
        {
            var response = await PostAsync(request, cancellationToken);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Post<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => PostAsync<T>(request)).GetAwaiter().GetResult();
        }

        private void CheckResponseContentType(HttpResponse response)
        {
            if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
            {
                throw new UnexpectedHtmlContentException(response);
            }
        }
    }
}
