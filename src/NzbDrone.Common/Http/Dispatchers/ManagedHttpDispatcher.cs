using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.HappyEyeballs;
using NzbDrone.Common.Http.Proxy;

namespace NzbDrone.Common.Http.Dispatchers
{
    public class ManagedHttpDispatcher : IHttpDispatcher
    {
        private const string NO_PROXY_KEY = "no-proxy";

        private readonly IHttpProxySettingsProvider _proxySettingsProvider;
        private readonly ICreateManagedWebProxy _createManagedWebProxy;
        private readonly ICertificateValidationService _certificateValidationService;
        private readonly IUserAgentBuilder _userAgentBuilder;
        private readonly ICached<System.Net.Http.HttpClient> _httpClientCache;
        private readonly ICached<CredentialCache> _credentialCache;
        private readonly HttpHappyEyeballs _httpHappyEyeballs;

        public ManagedHttpDispatcher(IHttpProxySettingsProvider proxySettingsProvider,
            ICreateManagedWebProxy createManagedWebProxy,
            ICertificateValidationService certificateValidationService,
            IUserAgentBuilder userAgentBuilder,
            ICacheManager cacheManager,
            Logger logger)
        {
            _proxySettingsProvider = proxySettingsProvider;
            _createManagedWebProxy = createManagedWebProxy;
            _certificateValidationService = certificateValidationService;
            _userAgentBuilder = userAgentBuilder;

            _httpClientCache = cacheManager.GetCache<System.Net.Http.HttpClient>(typeof(ManagedHttpDispatcher));
            _credentialCache = cacheManager.GetCache<CredentialCache>(typeof(ManagedHttpDispatcher), "credentialcache");

            _httpHappyEyeballs = new HttpHappyEyeballs(logger);
        }

        public Task<HttpResponse> GetResponseAsync(HttpRequest request, CookieContainer cookies)
        {
            return GetResponseAsync(request, cookies, CancellationToken.None);
        }

        public async Task<HttpResponse> GetResponseAsync(HttpRequest request, CookieContainer cookies, CancellationToken cancellationToken)
        {
            using var requestMessage = CreateRequestMessage(request, cookies);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(GetRequestTimeout(request));

            return await SendAsync(request, requestMessage, cts.Token);
        }

        private HttpRequestMessage CreateRequestMessage(HttpRequest request, CookieContainer cookies)
        {
            var requestMessage = new HttpRequestMessage(request.Method, (Uri)request.Url);
            requestMessage.Version = HttpVersion.Version20;
            requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            requestMessage.Headers.UserAgent.ParseAdd(_userAgentBuilder.GetUserAgent(request.UseSimplifiedUserAgent));
            requestMessage.Headers.ConnectionClose = !request.ConnectionKeepAlive;
            AddRequestCookies(requestMessage, request, cookies);
            AddRequestCredentials(requestMessage, request);

            if (request.ContentData != null)
            {
                requestMessage.Content = new ByteArrayContent(request.ContentData);
            }

            if (request.Headers != null)
            {
                AddRequestHeaders(requestMessage, request.Headers);
            }

            return requestMessage;
        }

        private void AddRequestCookies(HttpRequestMessage requestMessage, HttpRequest request, CookieContainer cookies)
        {
            var cookieHeader = cookies.GetCookieHeader((Uri)request.Url);
            if (cookieHeader.IsNotNullOrWhiteSpace())
            {
                requestMessage.Headers.Add("Cookie", cookieHeader);
            }
        }

        private void AddRequestCredentials(HttpRequestMessage requestMessage, HttpRequest request)
        {
            if (request.Credentials != null)
            {
                if (request.Credentials is BasicNetworkCredential bc)
                {
                    // Manually set header to avoid initial challenge response
                    var authInfo = bc.UserName + ":" + bc.Password;
                    authInfo = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(authInfo));
                    requestMessage.Headers.Add("Authorization", "Basic " + authInfo);
                }
                else if (request.Credentials is NetworkCredential nc)
                {
                    var creds = GetCredentialCache();
                    foreach (var authtype in new[] { "Basic", "Digest" })
                    {
                        creds.Remove((Uri)request.Url, authtype);
                        creds.Add((Uri)request.Url, authtype, nc);
                    }
                }
            }
        }

        private static TimeSpan GetRequestTimeout(HttpRequest request)
        {
            return request.RequestTimeout != TimeSpan.Zero ? request.RequestTimeout : TimeSpan.FromSeconds(100);
        }

        private async Task<HttpResponse> SendAsync(HttpRequest request, HttpRequestMessage requestMessage, CancellationToken cancellationToken)
        {
            var httpClient = GetClient(request.Url);

            try
            {
                using var responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var data = await ReadResponseDataAsync(request, responseMessage, cancellationToken);
                var headers = GetResponseHeaders(responseMessage);

                return new HttpResponse(request, new HttpHeader(headers), data, responseMessage.StatusCode, responseMessage.Version);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new WebException("Http request timed out", ex, WebExceptionStatus.Timeout, null);
            }
        }

        private static async Task<byte[]> ReadResponseDataAsync(HttpRequest request, HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            try
            {
                if (request.ResponseStream != null && responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    await responseMessage.Content.CopyToAsync(request.ResponseStream, null, cancellationToken);
                    return null;
                }

                return await responseMessage.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new WebException("Failed to read complete http response", ex, WebExceptionStatus.ReceiveFailure, null);
            }
        }

        private static HttpHeader GetResponseHeaders(HttpResponseMessage responseMessage)
        {
            var headers = responseMessage.Headers.ToNameValueCollection();
            headers.Add(responseMessage.Content.Headers.ToNameValueCollection());

            return new HttpHeader(headers);
        }

        protected virtual System.Net.Http.HttpClient GetClient(HttpUri uri)
        {
            var proxySettings = _proxySettingsProvider.GetProxySettings(uri);

            var key = proxySettings?.Key ?? NO_PROXY_KEY;

            return _httpClientCache.Get(key, () => CreateHttpClient(proxySettings));
        }

        protected virtual System.Net.Http.HttpClient CreateHttpClient(HttpProxySettings proxySettings)
        {
            var handler = new SocketsHttpHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
                UseCookies = false, // sic - we don't want to use a shared cookie container
                AllowAutoRedirect = false,
                Credentials = GetCredentialCache(),
                PreAuthenticate = true,
                MaxConnectionsPerServer = 12,
                ConnectCallback = Socket.OSSupportsIPv6 ? _httpHappyEyeballs.OnConnect : null,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = _certificateValidationService.ShouldByPassValidationError
                }
            };

            if (proxySettings != null)
            {
                handler.Proxy = _createManagedWebProxy.GetWebProxy(proxySettings);
            }

            var client = new System.Net.Http.HttpClient(handler)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                Timeout = Timeout.InfiniteTimeSpan
            };

            return client;
        }

        protected virtual void AddRequestHeaders(HttpRequestMessage webRequest, HttpHeader headers)
        {
            foreach (var header in headers)
            {
                switch (header.Key)
                {
                    case "Accept":
                        webRequest.Headers.Accept.ParseAdd(header.Value);
                        break;
                    case "Connection":
                        webRequest.Headers.Connection.Clear();
                        webRequest.Headers.Connection.Add(header.Value);
                        break;
                    case "Content-Length":
                        AddContentHeader(webRequest, "Content-Length", header.Value);
                        break;
                    case "Content-Type":
                        AddContentHeader(webRequest, "Content-Type", header.Value);
                        break;
                    case "Date":
                        webRequest.Headers.Remove("Date");
                        webRequest.Headers.Date = HttpHeader.ParseDateTime(header.Value);
                        break;
                    case "Expect":
                        webRequest.Headers.Expect.ParseAdd(header.Value);
                        break;
                    case "Host":
                        webRequest.Headers.Host = header.Value;
                        break;
                    case "If-Modified-Since":
                        webRequest.Headers.IfModifiedSince = HttpHeader.ParseDateTime(header.Value);
                        break;
                    case "Range":
                        throw new NotImplementedException();
                    case "Referer":
                        webRequest.Headers.Add("Referer", header.Value);
                        break;
                    case "Transfer-Encoding":
                        webRequest.Headers.TransferEncoding.ParseAdd(header.Value);
                        break;
                    case "User-Agent":
                        webRequest.Headers.UserAgent.ParseAdd(header.Value);
                        break;
                    case "Proxy-Connection":
                        throw new NotImplementedException();
                    default:
                        webRequest.Headers.Add(header.Key, header.Value);
                        break;
                }
            }
        }

        private static void AddContentHeader(HttpRequestMessage request, string header, string value)
        {
            var headers = request.Content?.Headers;
            if (headers == null)
            {
                return;
            }

            headers.Remove(header);
            headers.Add(header, value);
        }

        private CredentialCache GetCredentialCache()
        {
            return _credentialCache.Get("credentialCache", () => new CredentialCache());
        }
    }
}
