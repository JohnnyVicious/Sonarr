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

namespace NzbDrone.Api.Test.ApiInventory;

[TestFixture]
public class ApiOperationInventoryFixture
{
    private static readonly string[] KnownApiVersions = ["v3", "v5"];

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

    private static readonly Regex VersionedPathRegex = new("^/(?<root>api|feed)/(?<version>v[^/]*)(?<suffix>/|$)", RegexOptions.Compiled);

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
        var controllerAssemblies = LoadVersionedApiAssemblies().ToList();

        controllerAssemblies.Should().NotBeEmpty("versioned API assemblies should be present in the test output");

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
        var classificationManifest = LoadClassificationManifest();
        var classifiedOperations = classificationManifest.Operations;

        var duplicateEntries = classifiedOperations
            .GroupBy(operation => operation.Operation)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .OrderBy(key => key)
            .ToList();

        duplicateEntries.Should().BeEmpty("each OpenAPI operation should have exactly one classification entry");

        var invalidVersionFields = classifiedOperations
            .Where(operation => !StringComparer.Ordinal.Equals(operation.Version, operation.Operation.Version))
            .Select(operation => $"{operation.Operation}: version field is {operation.Version}, path version is {operation.Operation.Version}")
            .OrderBy(value => value)
            .ToList();

        invalidVersionFields.Should().BeEmpty("classification version fields should agree with operation path versions");

        var invalidClassifications = classifiedOperations
            .Where(operation => !classificationManifest.AllowedClassifications.Contains(operation.Classification))
            .Select(operation => $"{operation.Operation}: {operation.Classification}")
            .OrderBy(value => value)
            .ToList();

        invalidClassifications.Should().BeEmpty($"allowed classifications are: {string.Join(", ", classificationManifest.AllowedClassifications.OrderBy(v => v))}");

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

    [Test]
    public void common_v3_v5_operations_should_have_explicit_compatibility_classifications()
    {
        var currentOperations = LoadCurrentApiOperations();
        var classificationManifest = LoadClassificationManifest();
        var classifiedByOperation = classificationManifest.Operations.ToDictionary(operation => operation.Operation);
        var operationsByVersion = currentOperations
            .GroupBy(operation => operation.Version)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(operation => new NormalizedOperation(operation.Method, NormalizeVersionedPath(operation.Path))));

        operationsByVersion.Keys.Should().Contain(KnownApiVersions, "the compatibility matrix requires both current API versions");

        var commonOperations = operationsByVersion["v3"].Keys
            .Intersect(operationsByVersion["v5"].Keys)
            .OrderBy(operation => operation.ToString(), StringComparer.Ordinal)
            .ToList();

        commonOperations.Should().HaveCount(148, "the current shared v3/v5 OpenAPI surface should not drift silently");

        var missingClassifications = commonOperations
            .SelectMany(operation => KnownApiVersions.Select(version => operationsByVersion[version][operation]))
            .Where(operation => !classifiedByOperation.ContainsKey(operation))
            .Select(operation => operation.ToString())
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToList();

        missingClassifications.Should().BeEmpty("every shared operation needs a classification before it can be compatibility-gated");

        var invalidCompatibilityPairs = commonOperations
            .Select(operation => new
            {
                Operation = operation,
                V3Classification = classifiedByOperation[operationsByVersion["v3"][operation]].Classification,
                V5Classification = classifiedByOperation[operationsByVersion["v5"][operation]].Classification
            })
            .Where(pair => !IsAllowedCommonClassificationPair(pair.V3Classification, pair.V5Classification))
            .Select(pair => $"{pair.Operation}: v3={pair.V3Classification}, v5={pair.V5Classification}")
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToList();

        invalidCompatibilityPairs.Should().BeEmpty("shared routes should either be v3 compatibility plus v5 primary, or carry the same explicit mock/destructive/exclusion classification");
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

    private static ClassificationManifest LoadClassificationManifest()
    {
        var classificationPath = InventoryPath("api-operation-classification.json");
        var document = JsonNode.Parse(File.ReadAllText(classificationPath))?.AsObject()
                       ?? throw new InvalidOperationException($"Unable to parse {classificationPath}");

        var allowedClassificationNodes = document["allowedClassifications"]?.AsArray()
                                         ?? throw new InvalidOperationException($"{classificationPath} is missing an allowedClassifications array");
        var allowedClassifications = allowedClassificationNodes
            .Select(node => node?.GetValue<string>()
                            ?? throw new InvalidOperationException($"{classificationPath} contains a non-string allowed classification entry"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        allowedClassifications.Should().NotBeEmpty($"{classificationPath} should define allowed classification values");

        var operations = document["operations"]?.AsArray()
                         ?? throw new InvalidOperationException($"{classificationPath} is missing an operations array");

        var classifiedOperations = operations
            .Select(operationNode =>
            {
                var operation = operationNode?.AsObject()
                                ?? throw new InvalidOperationException($"{classificationPath} contains a non-object operation entry");

                var version = RequiredString(operation, "version", classificationPath);
                var method = RequiredString(operation, "method", classificationPath);
                var path = RequiredString(operation, "path", classificationPath);
                var classification = RequiredString(operation, "classification", classificationPath);

                return new ClassifiedOperation(new ApiOperation(method, path), version, classification);
            })
            .ToList();

        return new ClassificationManifest(allowedClassifications, classifiedOperations);
    }

    private static bool IsAllowedCommonClassificationPair(string v3Classification, string v5Classification)
    {
        if (StringComparer.Ordinal.Equals(v3Classification, "v3-compat") &&
            StringComparer.Ordinal.Equals(v5Classification, "v5-primary"))
        {
            return true;
        }

        return StringComparer.Ordinal.Equals(v3Classification, v5Classification) &&
               (StringComparer.Ordinal.Equals(v3Classification, "destructive-smoke-only") ||
                StringComparer.Ordinal.Equals(v3Classification, "external-provider-mocked") ||
                StringComparer.Ordinal.Equals(v3Classification, "manual/excluded-with-rationale"));
    }

    private static IEnumerable<OpenApiDocument> LoadOpenApiDocuments()
    {
        return Directory.EnumerateFiles(InventoryDirectory(), "openapi.*.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(LoadOpenApiDocument);
    }

    private static OpenApiDocument LoadOpenApiDocument(string documentPath)
    {
        var document = JsonNode.Parse(File.ReadAllText(documentPath))?.AsObject()
                       ?? throw new InvalidOperationException($"Unable to parse {documentPath}");

        var paths = document["paths"]?.AsObject()
                    ?? throw new InvalidOperationException($"{documentPath} is missing a paths object");

        var allPaths = paths.Select(path => path.Key).ToList();
        var operations = paths
            .Where(path => VersionedPathRegex.IsMatch(path.Key))
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

    private static IEnumerable<Assembly> LoadVersionedApiAssemblies()
    {
        return Directory.EnumerateFiles(TestContext.CurrentContext.TestDirectory, "Sonarr.Api.V*.dll")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(Assembly.LoadFrom);
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

    private static string NormalizeVersionedPath(string path)
    {
        return VersionedPathRegex.Replace(
            path,
            match => $"/{match.Groups["root"].Value}/{{version}}{match.Groups["suffix"].Value}",
            1);
    }

    private static string InventoryPath(string fileName)
    {
        return Path.Combine(InventoryDirectory(), fileName);
    }

    private static string InventoryDirectory()
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "ApiInventory");
    }

    private sealed record OpenApiDocument(List<string> Paths, List<ApiOperation> ApiOperations);

    private sealed record ClassificationManifest(HashSet<string> AllowedClassifications, List<ClassifiedOperation> Operations);

    private sealed record ClassifiedOperation(ApiOperation Operation, string Version, string Classification);

    private sealed record NormalizedOperation(string Method, string Path)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }

    private sealed record ApiOperation
    {
        public ApiOperation(string method, string path)
        {
            Method = method.ToUpperInvariant();
            Path = path;
            Version = ExtractVersion(path);
        }

        public string Method { get; }
        public string Path { get; }
        public string Version { get; }

        public override string ToString()
        {
            return $"{Method} {Path}";
        }

        private static string ExtractVersion(string path)
        {
            var match = VersionedPathRegex.Match(path);

            if (!match.Success)
            {
                throw new InvalidOperationException($"{path} is not a versioned API or feed path");
            }

            return match.Groups["version"].Value;
        }
    }
}
