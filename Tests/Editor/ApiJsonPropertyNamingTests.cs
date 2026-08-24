using System.Collections.Generic;
using System.Text;
using Deucarian.API.Certificates;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace Deucarian.API.Tests
{
    public sealed class ApiJsonPropertyNamingTests
    {
        [Test]
        public void ClientJsonNamingDefaultsToSnakeCase()
        {
            var options = new ApiJsonSerializerOptions();
            var serializer = new NewtonsoftApiSerializer(options);

            string json = serializer.Serialize(CreateDto());

            Assert.That(options.PropertyNaming,
                        Is.EqualTo(ApiJsonPropertyNamingPolicy.SnakeCase));
            Assert.That(json, Does.Contain("\"project_display_name\""));
            Assert.That(json, Does.Contain("\"explicit_backend_name\""));
            Assert.That(json, Does.Not.Contain("ProjectDisplayName"));
        }

        [TestCase(ApiJsonPropertyNamingPolicy.SnakeCase, "project_display_name")]
        [TestCase(ApiJsonPropertyNamingPolicy.CamelCase, "projectDisplayName")]
        [TestCase(ApiJsonPropertyNamingPolicy.PascalCase, "ProjectDisplayName")]
        [TestCase(ApiJsonPropertyNamingPolicy.KebabCase, "project-display-name")]
        [TestCase(ApiJsonPropertyNamingPolicy.ScreamingSnakeCase, "PROJECT_DISPLAY_NAME")]
        [TestCase(ApiJsonPropertyNamingPolicy.AsDeclared, "ProjectDisplayName")]
        public void SupportsEveryPropertyNamingPolicy(
            ApiJsonPropertyNamingPolicy policy,
            string expectedName)
        {
            var options = new ApiJsonSerializerOptions
            {
                    PropertyNaming = policy
            };
            var serializer = new NewtonsoftApiSerializer(options);

            string json = serializer.Serialize(CreateDto());
            NamingDto roundTrip = serializer.Deserialize<NamingDto>(json);

            Assert.That(json, Does.Contain("\"" + expectedName + "\""));
            Assert.That(json, Does.Contain("\"explicit_backend_name\""));
            Assert.That(roundTrip.ProjectDisplayName, Is.EqualTo("Viewer"));
            Assert.That(roundTrip.ExplicitName, Is.EqualTo("Preserved"));
        }

        [Test]
        public void NamingPolicyDoesNotRewriteDictionaryKeys()
        {
            var serializer = new NewtonsoftApiSerializer();
            var dto = CreateDto();
            dto.Metadata["External_Key"] = "value";

            string json = serializer.Serialize(dto);

            Assert.That(json, Does.Contain("\"metadata\""));
            Assert.That(json, Does.Contain("\"External_Key\""));
        }

        [Test]
        public void EndpointOverrideTravelsToRequest()
        {
            ApiEndpointDefinition definition =
                    ScriptableObject.CreateInstance<ApiEndpointDefinition>();
            try
            {
                definition.Path = "projects";
                definition.Method = HttpMethod.POST;
                definition.OverrideJsonPropertyNaming = true;
                definition.JsonPropertyNaming =
                        ApiJsonPropertyNamingPolicy.CamelCase;

                ApiRequest request = definition.CreateRequest(CreateDto());

                Assert.That(request.JsonPropertyNamingOverride,
                            Is.EqualTo(ApiJsonPropertyNamingPolicy.CamelCase));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RequestOverrideControlsSerializedBodyWithoutChangingClientDefault()
        {
            ApiClientConfig config = ApiClientConfig.CreateRuntimeDefault();
            config.BaseUrl = "https://example.com";
            var serializer = new NewtonsoftApiSerializer(
                    config.JsonSerializerSettings);
            var builder = new UnityWebRequestBuilder(
                    config,
                    serializer,
                    null,
                    new ApiCertificateHandlerFactory(
                            ApiCertificateHandlingMode.DefaultValidation));
            var request = new ApiRequest("projects", HttpMethod.POST)
            {
                    Body = CreateDto(),
                    JsonPropertyNamingOverride =
                            ApiJsonPropertyNamingPolicy.CamelCase
            };

            try
            {
                using (UnityWebRequest webRequest =
                       builder.BuildAsync(
                                      request,
                                      ApiResponseFormat.Json,
                                      default)
                              .GetAwaiter()
                              .GetResult())
                {
                    string json = Encoding.UTF8.GetString(
                            webRequest.uploadHandler.data);
                    Assert.That(json, Does.Contain("\"projectDisplayName\""));
                    Assert.That(json, Does.Not.Contain("\"project_display_name\""));
                }

                Assert.That(config.JsonSerializerSettings.PropertyNaming,
                            Is.EqualTo(ApiJsonPropertyNamingPolicy.SnakeCase));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResponseUsesTheSameRequestOverride()
        {
            var parser = new ApiResponseParser(
                    new NewtonsoftApiSerializer());
            var request = new ApiRequest("projects", HttpMethod.GET)
            {
                    JsonPropertyNamingOverride =
                            ApiJsonPropertyNamingPolicy.KebabCase
            };
            var response = new ApiTransportResponse
            {
                    StatusCode = 200,
                    RequestUrl = "https://example.com/projects",
                    RawBody = "{\"project-display-name\":\"Viewer\",\"explicit_backend_name\":\"Preserved\"}",
                    UnityResult = UnityWebRequest.Result.Success
            };

            ApiResult<NamingDto> result = parser.Parse<NamingDto>(
                    request,
                    response,
                    ApiResponseFormat.Json);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data.ProjectDisplayName, Is.EqualTo("Viewer"));
            Assert.That(result.Data.ExplicitName, Is.EqualTo("Preserved"));
        }

        private static NamingDto CreateDto()
        {
            return new NamingDto
            {
                    ProjectDisplayName = "Viewer",
                    ExplicitName = "Preserved"
            };
        }

        private sealed class NamingDto
        {
            public string ProjectDisplayName { get; set; }

            [JsonProperty("explicit_backend_name")]
            public string ExplicitName { get; set; }

            public Dictionary<string, string> Metadata { get; } =
                    new Dictionary<string, string>();
        }
    }
}
