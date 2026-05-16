using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using Sonarr.Http;
using V3SeriesController = Sonarr.Api.V3.Series.SeriesController;
using V5SeriesController = Sonarr.Api.V5.Series.SeriesController;

namespace NzbDrone.Api.Test.ApiInventory;

[TestFixture]
public class ApiOperationInventoryFixture
{
    private static readonly string[] KnownApiVersions = ["v3", "v5"];

    private static readonly HashSet<string> AllowedClassifications = new(StringComparer.Ordinal)
    {
        "v5-primary",
        "v5-only",
        "v3-compat",
        "v3-only",
        "destructive-smoke-only",
        "external-provider-mocked",
        "manual/excluded-with-rationale"
    };

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get",
        "put",
        "post",
        "delete",
        "patch",
        "head",
        "options",
        "trace"
    };

    private static readonly Regex VersionedPathRegex = new("^/(?:api|feed)/(?<version>v\\d+)(?:/|$)", RegexOptions.Compiled);

    [Test]
    public void openapi_specs_should_only_expose_known_api_versions()
    {
        var versions = LoadOpenApiDocuments()
            .SelectMany(document => document.Paths)
            .Select(path => VersionedPathRegex.Match(path))
            .Where(match => match.Success)
            .Select(match => match.Groups["version"].Value)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        versions.Should().Equal(KnownApiVersions, "new API versions need an explicit test plan and inventory classification");
    }

    [Test]
    public void versioned_route_attributes_should_only_use_known_api_versions()
    {
        var controllerAssemblies = new[]
        {
            typeof(V3SeriesController).Assembly,
            typeof(V5SeriesController).Assembly
        };

        var versions = controllerAssemblies
            .SelectMany(GetControllerRouteVersions)
            .Select(version => $"v{version}")
            .Distinct()
            .OrderBy(version => version)
            .ToList();

        versions.Should().Equal(KnownApiVersions, "new versioned controllers need an explicit test plan and inventory classification");
    }

    [Test]
    public void operation_classification_should_cover_current_openapi_operations()
    {
        var currentOperations = LoadCurrentApiOperations();
        var classifiedOperations = LoadClassifiedOperations();

        var duplicateEntries = classifiedOperations
            .GroupBy(operation => operation.Operation)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .OrderBy(key => key)
            .ToList();

        duplicateEntries.Should().BeEmpty("each OpenAPI operation should have exactly one classification entry");

        var invalidClassifications = classifiedOperations
            .Where(operation => !AllowedClassifications.Contains(operation.Classification))
            .Select(operation => $"{operation.Operation}: {operation.Classification}")
            .OrderBy(value => value)
            .ToList();

        invalidClassifications.Should().BeEmpty($"allowed classifications are: {string.Join(", ", AllowedClassifications.OrderBy(v => v))}");

        var classifiedOperationSet = classifiedOperations
            .Select(operation => operation.Operation)
            .ToHashSet();

        var missingClassifications = currentOperations
            .Where(operation => !classifiedOperationSet.Contains(operation))
            .Select(operation => operation.ToString())
            .OrderBy(operation => operation)
            .ToList();

        var staleClassifications = classifiedOperationSet
            .Where(operation => !currentOperations.Contains(operation))
            .Select(operation => operation.ToString())
            .OrderBy(operation => operation)
            .ToList();

        missingClassifications.Should().BeEmpty("new OpenAPI operations must be classified before endpoint coverage can proceed");
        staleClassifications.Should().BeEmpty("removed OpenAPI operations should be removed from the classification file");
    }

    private static IEnumerable<int> GetControllerRouteVersions(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .SelectMany(type =>
                type.GetCustomAttributes<VersionedApiControllerAttribute>(true).Select(attribute => attribute.Version)
                    .Concat(type.GetCustomAttributes<VersionedFeedControllerAttribute>(true).Select(attribute => attribute.Version)));
    }

    private static HashSet<ApiOperation> LoadCurrentApiOperations()
    {
        return LoadOpenApiDocuments()
            .SelectMany(document => document.ApiOperations)
            .ToHashSet();
    }

    private static List<ClassifiedOperation> LoadClassifiedOperations()
    {
        var classificationPath = InventoryPath("api-operation-classification.json");
        var document = JsonNode.Parse(File.ReadAllText(classificationPath))?.AsObject()
                       ?? throw new InvalidOperationException($"Unable to parse {classificationPath}");

        var operations = document["operations"]?.AsArray()
                         ?? throw new InvalidOperationException($"{classificationPath} is missing an operations array");

        return operations
            .Select(operationNode =>
            {
                var operation = operationNode?.AsObject()
                                ?? throw new InvalidOperationException($"{classificationPath} contains a non-object operation entry");

                var method = RequiredString(operation, "method", classificationPath);
                var path = RequiredString(operation, "path", classificationPath);
                var classification = RequiredString(operation, "classification", classificationPath);

                return new ClassifiedOperation(new ApiOperation(method, path), classification);
            })
            .ToList();
    }

    private static IEnumerable<OpenApiDocument> LoadOpenApiDocuments()
    {
        yield return LoadOpenApiDocument("openapi.v3.json");
        yield return LoadOpenApiDocument("openapi.v5.json");
    }

    private static OpenApiDocument LoadOpenApiDocument(string fileName)
    {
        var documentPath = InventoryPath(fileName);
        var document = JsonNode.Parse(File.ReadAllText(documentPath))?.AsObject()
                       ?? throw new InvalidOperationException($"Unable to parse {documentPath}");

        var paths = document["paths"]?.AsObject()
                    ?? throw new InvalidOperationException($"{documentPath} is missing a paths object");

        var allPaths = paths.Select(path => path.Key).ToList();
        var operations = paths
            .Where(path => path.Key.StartsWith("/api/v", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                var pathItem = path.Value?.AsObject()
                               ?? throw new InvalidOperationException($"{documentPath} contains a non-object path item for {path.Key}");

                return pathItem
                    .Where(method => HttpMethods.Contains(method.Key))
                    .Select(method => new ApiOperation(method.Key, path.Key));
            })
            .ToList();

        return new OpenApiDocument(allPaths, operations);
    }

    private static string RequiredString(JsonObject operation, string propertyName, string classificationPath)
    {
        var value = operation[propertyName]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{classificationPath} contains an operation entry without {propertyName}");
        }

        return value;
    }

    private static string InventoryPath(string fileName)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "ApiInventory", fileName);
    }

    private sealed record OpenApiDocument(List<string> Paths, List<ApiOperation> ApiOperations);

    private sealed record ClassifiedOperation(ApiOperation Operation, string Classification);

    private sealed record ApiOperation
    {
        public ApiOperation(string method, string path)
        {
            Method = method.ToUpperInvariant();
            Path = path;
        }

        public string Method { get; }
        public string Path { get; }

        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }
}
