using System;
using System.IO;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using RestSharp;
using JsonNode = System.Text.Json.Nodes.JsonNode;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace NzbDrone.Integration.Test.Client
{
    public sealed class OpenApiSpecification
    {
        private readonly JsonObject _paths;

        private OpenApiSpecification(string version, JsonObject paths)
        {
            Version = version;
            _paths = paths;
        }

        public string Version { get; }

        public static OpenApiSpecification Load(string version)
        {
            var normalizedVersion = NormalizeVersion(version);
            var specPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "ApiInventory", $"openapi.{normalizedVersion}.json");
            var document = JsonNode.Parse(File.ReadAllText(specPath))?.AsObject()
                           ?? throw new InvalidOperationException($"Unable to parse {specPath}");
            var paths = document["paths"]?.AsObject()
                        ?? throw new InvalidOperationException($"{specPath} is missing a paths object");

            return new OpenApiSpecification(normalizedVersion, paths);
        }

        public void ShouldDeclareOperation(Method method, string path)
        {
            GetOperation(method, path);
        }

        public void ShouldDeclareResponse(Method method, string path, HttpStatusCode statusCode)
        {
            var operation = GetOperation(method, path);
            var responses = operation["responses"]?.AsObject();

            responses.Should().NotBeNull($"{method} {NormalizePath(path)} should declare OpenAPI responses");
            responses.ContainsKey(((int)statusCode).ToString()).Should().BeTrue($"{method} {NormalizePath(path)} should declare a {(int)statusCode} response");
        }

        public void ShouldMatchDeclaredResponse(Method method, string path, IRestResponse response)
        {
            ShouldDeclareResponse(method, path, response.StatusCode);
        }

        public JsonObject GetResponseSchema(Method method, string path, HttpStatusCode statusCode, string contentType = "application/json")
        {
            var operation = GetOperation(method, path);
            var schema = operation["responses"]?[((int)statusCode).ToString()]?["content"]?[contentType]?["schema"] as JsonObject;

            schema.Should().NotBeNull($"{method} {NormalizePath(path)} should declare a {contentType} schema for {(int)statusCode}");

            return schema;
        }

        public string ApiPath(string resource)
        {
            if (resource.StartsWith("/", StringComparison.Ordinal))
            {
                return resource;
            }

            return $"/api/{Version}/{resource.TrimStart('/')}";
        }

        private JsonObject GetOperation(Method method, string path)
        {
            var normalizedPath = NormalizePath(path);
            var pathItem = _paths[normalizedPath] as JsonObject;

            pathItem.Should().NotBeNull($"the {Version} OpenAPI spec should declare {normalizedPath}");

            var operation = pathItem[method.ToString().ToLowerInvariant()] as JsonObject;

            operation.Should().NotBeNull($"the {Version} OpenAPI spec should declare {method} {normalizedPath}");

            return operation;
        }

        private string NormalizePath(string path)
        {
            return ApiPath(path);
        }

        private static string NormalizeVersion(string version)
        {
            return version.StartsWith("v", StringComparison.Ordinal) ? version : $"v{version}";
        }
    }
}
