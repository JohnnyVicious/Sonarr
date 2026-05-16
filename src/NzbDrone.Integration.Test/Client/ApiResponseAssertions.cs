using System.Linq;
using System.Net;
using FluentAssertions;
using RestSharp;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using JsonNode = System.Text.Json.Nodes.JsonNode;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace NzbDrone.Integration.Test.Client
{
    public static class ApiResponseAssertions
    {
        public static IRestResponse ShouldHaveStatusCode(this IRestResponse response, HttpStatusCode statusCode)
        {
            if (response.ErrorException != null)
            {
                throw response.ErrorException;
            }

            response.ErrorMessage.Should().BeNullOrWhiteSpace();
            response.StatusCode.Should().Be(statusCode, response.Content ?? string.Empty);

            return response;
        }

        public static IRestResponse ShouldDisableCache(this IRestResponse response)
        {
            var headers = response.Headers;
            ((string)headers.Single(header => header.Name == "Cache-Control").Value).Split(',').Select(header => header.Trim())
                .Should().BeEquivalentTo("no-store, no-cache".Split(',').Select(header => header.Trim()));
            headers.Single(header => header.Name == "Pragma").Value.Should().Be("no-cache");
            headers.Single(header => header.Name == "Expires").Value.Should().Be("-1");

            return response;
        }

        public static JsonNode ShouldHaveJsonContent(this IRestResponse response)
        {
            response.Content.Should().NotBeNullOrWhiteSpace();
            response.ContentType.Should().StartWith("application/json");

            var json = JsonNode.Parse(response.Content);
            json.Should().NotBeNull("API responses that declare JSON should contain valid JSON");

            return json;
        }

        public static JsonObject ShouldHaveJsonObjectContent(this IRestResponse response)
        {
            var json = response.ShouldHaveJsonContent();
            var jsonObject = json as JsonObject;

            jsonObject.Should().NotBeNull("the response should be a JSON object");

            return jsonObject;
        }

        public static JsonArray ShouldHaveJsonArrayContent(this IRestResponse response)
        {
            var json = response.ShouldHaveJsonContent();
            var jsonArray = json as JsonArray;

            jsonArray.Should().NotBeNull("the response should be a JSON array");

            return jsonArray;
        }

        public static JsonArray ShouldHaveValidationErrors(this IRestResponse response)
        {
            response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);

            var errors = response.ShouldHaveJsonArrayContent();
            errors.Should().NotBeEmpty("validation responses should include at least one error");

            var invalidErrors = errors
                .Select((error, index) => new
                {
                    Index = index,
                    Error = error as JsonObject
                })
                .Where(error => error.Error == null ||
                                error.Error["propertyName"] == null ||
                                error.Error["errorMessage"] == null)
                .Select(error => error.Index)
                .ToList();

            invalidErrors.Should().BeEmpty("validation errors should include propertyName and errorMessage");

            return errors;
        }
    }
}
