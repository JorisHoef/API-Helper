using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.API.Certificates;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.API.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace Deucarian.API.Tests
{
    public sealed class ApiClientTests
    {
        private static class TestRoutes
        {
            public const string Projects = "projects";
        }

        [Test]
        public void ApiClientConfig_UsesSafeCertificateDefault()
        {
            ApiClientConfig config = ScriptableObject.CreateInstance<ApiClientConfig>();
            ApiCertificateHandlerFactory factory =
                    new ApiCertificateHandlerFactory(config.CertificateHandlingMode);

            Assert.AreEqual(ApiCertificateHandlingMode.DefaultValidation, config.CertificateHandlingMode);
            Assert.AreEqual(ApiAuthenticationMode.None, config.AuthenticationMode);
            Assert.AreEqual(ApiResponseFormat.Auto, config.DefaultResponseFormat);
            Assert.IsNull(factory.CreateFor("https://example.com"));
        }

        [Test]
        public void ApiTransportResponse_IsInternalPipelineType()
        {
            Assert.IsFalse(typeof(ApiTransportResponse).IsPublic);
            Assert.IsFalse(typeof(ApiTransportResponse).IsNestedPublic);
        }

        [Test]
        public void ApiRequest_DoesNotStoreExpectedResponseType()
        {
            Assert.IsNull(typeof(ApiRequest).GetProperty("ExpectedResponseType"));
            Assert.IsNull(typeof(ApiRequest).Assembly.GetType("Deucarian.API.Models.ApiRequest`1"));
        }

        [Test]
        public void ApiClient_UsesRawStringEndpointConstants()
        {
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder, new SuccessRequestSender("[]"));

            ApiResult<List<string>> result =
                    client.GetAsync<List<string>>(TestRoutes.Projects, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(TestRoutes.Projects, builder.LastRequest.Endpoint);
            Assert.AreEqual(HttpMethod.GET, builder.LastRequest.Method);
        }

        [Test]
        public void ApiClient_UsesApiEndpointMethodAndAuth()
        {
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder, new SuccessRequestSender("{}"));
            ApiEndpoint endpoint = new ApiEndpoint("projects",
                                                   HttpMethod.POST,
                                                   ApiAuthenticationRequirement.Required,
                                                   timeoutSeconds: 5,
                                                   defaultHeaders: new Dictionary<string, string>
                                                   {
                                                           { "X-Client", "Unity" }
                                                   },
                                                   defaultQueryParameters: new Dictionary<string, string>
                                                   {
                                                           { "include", "users" }
                                                   });

            client.SendAsync<Dictionary<string, string>>(endpoint,
                                                         new { name = "Project" },
                                                         CancellationToken.None)
                  .GetAwaiter()
                  .GetResult();

            Assert.AreEqual("projects", builder.LastRequest.Endpoint);
            Assert.AreEqual(HttpMethod.POST, builder.LastRequest.Method);
            Assert.AreEqual(ApiAuthenticationRequirement.Required, builder.LastRequest.Authentication);
            Assert.AreEqual(5, builder.LastRequest.TimeoutSeconds);
            Assert.AreEqual("Unity", builder.LastRequest.Headers["X-Client"]);
            Assert.AreEqual("users", builder.LastRequest.QueryParameters["include"]);
            Assert.NotNull(builder.LastRequest.Body);
        }

        [Test]
        public void ApiEndpoint_ReplacesAndEscapesPathParameters()
        {
            ApiEndpoint endpoint =
                    new ApiEndpoint("projects/{id}", HttpMethod.GET)
                            .WithPathParameter("id", "A B");

            ApiRequest request = endpoint.CreateRequest();

            Assert.AreEqual("projects/A%20B", request.Endpoint);
        }

        [Test]
        public void ApiEndpoint_FailsClearlyWhenPathParameterIsMissing()
        {
            ApiEndpoint endpoint = new ApiEndpoint("projects/{id}", HttpMethod.GET);

            InvalidOperationException ex =
                    Assert.Throws<InvalidOperationException>(() => endpoint.CreateRequest());

            Assert.That(ex.Message, Does.Contain("{id}"));
        }

        [Test]
        public void ResponseParser_TreatsEmptyArrayAsSuccessfulResponse()
        {
            ApiResponseParser parser =
                    new ApiResponseParser(new NewtonsoftApiSerializer());
            ApiRequest request = new ApiRequest("projects", HttpMethod.GET);
            ApiTransportResponse response = new ApiTransportResponse
            {
                    StatusCode = 200,
                    RequestUrl = "https://example.com/projects",
                    RawBody = "[]",
                    UnityResult = UnityWebRequest.Result.Success
            };

            ApiResult<List<string>> result = parser.Parse<List<string>>(request,
                                                                        response,
                                                                        ApiResponseFormat.Json);

            Assert.IsTrue(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.AreEqual(0, result.Data.Count);
        }

        [Test]
        public void RequestBuilder_InjectsBearerAuthHeader()
        {
            ApiClientConfig config = ScriptableObject.CreateInstance<ApiClientConfig>();
            config.BaseUrl = "https://example.com";
            config.AuthenticationMode = ApiAuthenticationMode.BearerToken;

            UnityWebRequestBuilder builder = new UnityWebRequestBuilder(
                    config,
                    new NewtonsoftApiSerializer(),
                    new StaticBearerTokenProvider("test-token"),
                    new ApiCertificateHandlerFactory(ApiCertificateHandlingMode.DefaultValidation));

            ApiRequest request = new ApiRequest("projects",
                                                HttpMethod.GET,
                                                ApiAuthenticationRequirement.Required);

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Json, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("https://example.com/projects", webRequest.url);
                Assert.AreEqual("Bearer test-token", webRequest.GetRequestHeader("Authorization"));
                Assert.AreEqual("application/json", webRequest.GetRequestHeader("Accept"));
            }
        }

        [Test]
        public void RequestBuilder_AllowsOptionalAuthWithoutProvider()
        {
            ApiClientConfig config = ScriptableObject.CreateInstance<ApiClientConfig>();
            config.BaseUrl = "https://example.com";

            UnityWebRequestBuilder builder = new UnityWebRequestBuilder(
                    config,
                    new NewtonsoftApiSerializer(),
                    null,
                    new ApiCertificateHandlerFactory(ApiCertificateHandlingMode.DefaultValidation));

            ApiRequest request = new ApiRequest("projects",
                                                HttpMethod.GET,
                                                ApiAuthenticationRequirement.Optional);

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Json, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.IsNull(webRequest.GetRequestHeader("Authorization"));
            }
        }

        [Test]
        public void ErrorParser_ExtractsBackendAndValidationErrors()
        {
            ApiErrorParser parser = new ApiErrorParser();
            ApiRequest request = new ApiRequest("projects", HttpMethod.POST);
            ApiTransportResponse response = new ApiTransportResponse
            {
                    StatusCode = 422,
                    RequestUrl = "https://example.com/projects",
                    RawBody = "{\"code\":\"validation_failed\",\"message\":\"Invalid payload\",\"errors\":{\"name\":[\"Required\"]}}",
                    TransportError = "HTTP/1.1 422 Unprocessable Entity",
                    ResponseHeaders = new Dictionary<string, string>
                    {
                            { "X-Trace-Id", "abc-123" }
                    }
            };

            ApiError error = parser.Parse(request, response);

            Assert.AreEqual(422, error.HttpStatusCode);
            Assert.AreEqual("https://example.com/projects", error.RequestUrl);
            Assert.AreEqual("abc-123", error.ResponseHeaders["X-Trace-Id"]);
            Assert.AreEqual("validation_failed", error.BackendCode);
            Assert.AreEqual("Invalid payload", error.BackendMessage);
            Assert.AreEqual("Required", error.ValidationErrors["name"][0]);
        }

        [Test]
        public void RequestBuilder_AppliesTimeoutOverride()
        {
            ApiClientConfig config = ScriptableObject.CreateInstance<ApiClientConfig>();
            config.BaseUrl = "https://example.com";
            config.TimeoutSeconds = 12;

            UnityWebRequestBuilder builder = new UnityWebRequestBuilder(
                    config,
                    new NewtonsoftApiSerializer(),
                    null,
                    new ApiCertificateHandlerFactory(ApiCertificateHandlingMode.DefaultValidation));

            ApiRequest request = new ApiRequest("projects", HttpMethod.GET)
            {
                    TimeoutSeconds = 3
            };

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Json, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual(3, webRequest.timeout);
            }
        }

        [Test]
        public void ApiClient_ReturnsCancellationError()
        {
            ApiClientConfig config = ApiClientConfig.CreateRuntimeDefault();
            config.LoggingMode = ApiLoggingMode.None;

            ApiClient client = CreateTestClient(new RecordingRequestBuilder(),
                                                new CancellingRequestSender(),
                                                config: config);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();

                ApiResult<string> result = client.GetAsync<string>("projects", cts.Token)
                                                  .GetAwaiter()
                                                  .GetResult();

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.Error.IsCancellation);
            }
        }

        [Test]
        public void ApiClient_SuppressesAllLoggingForSensitiveRequest()
        {
            ApiClientConfig config = ApiClientConfig.CreateRuntimeDefault();
            config.LoggingMode = ApiLoggingMode.Verbose;
            config.LogRawJson = true;
            bool previousGlobalRawJson = APIDebugSettings.LogRawJson;
            APIDebugSettings.LogRawJson = true;

            try
            {
                ApiClient client = CreateTestClient(
                    new RecordingRequestBuilder(),
                    new SuccessRequestSender(
                        "{\"access_token\":\"synthetic-test-token\"}"),
                    config: config);
                ApiRequest request = new ApiRequest(
                    "https://auth.example.invalid/login",
                    HttpMethod.POST,
                    ApiAuthenticationRequirement.Disabled)
                {
                    Body = new { password = "synthetic-test-secret" },
                    SuppressLogging = true
                };

                ApiResult<Dictionary<string, string>> result =
                    client.SendAsync<Dictionary<string, string>>(
                            request,
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                Assert.IsTrue(result.IsSuccess);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                APIDebugSettings.LogRawJson = previousGlobalRawJson;
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ApiClient_SuppressesSensitiveFailureLogging()
        {
            ApiClientConfig config = ApiClientConfig.CreateRuntimeDefault();
            config.LoggingMode = ApiLoggingMode.Verbose;

            try
            {
                ApiClient client = CreateTestClient(
                    new RecordingRequestBuilder(),
                    new FailureRequestSender(
                        401,
                        "{\"message\":\"synthetic sensitive failure\"}"),
                    config: config);
                ApiRequest request = new ApiRequest(
                    "https://auth.example.invalid/login?identity=hidden",
                    HttpMethod.POST,
                    ApiAuthenticationRequirement.Disabled)
                {
                    SuppressLogging = true
                };

                ApiResult<Dictionary<string, string>> result =
                    client.SendAsync<Dictionary<string, string>>(
                            request,
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(401, result.HttpStatusCode);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EndpointDefinition_ConvertsToSharedEndpoint()
        {
            ApiEndpointDefinition endpoint = ScriptableObject.CreateInstance<ApiEndpointDefinition>();
            endpoint.DisplayName = "Projects";
            endpoint.Path = "projects";
            endpoint.Method = HttpMethod.GET;
            endpoint.Authentication = ApiAuthenticationRequirement.Required;
            endpoint.TimeoutOverrideSeconds = 7;
            endpoint.DefaultHeaders.Add(new ApiKeyValuePair { Key = "X-Client", Value = "Unity" });
            endpoint.DefaultQueryParameters.Add(new ApiKeyValuePair { Key = "include", Value = "users" });

            ApiEndpoint codeEndpoint = endpoint.ToEndpoint();
            ApiRequest request = endpoint.CreateRequest();

            Assert.AreEqual("Projects", endpoint.DisplayName);
            Assert.AreEqual("projects", codeEndpoint.Path);
            Assert.AreEqual("projects", request.Endpoint);
            Assert.AreEqual(HttpMethod.GET, request.Method);
            Assert.AreEqual(ApiAuthenticationRequirement.Required, request.Authentication);
            Assert.AreEqual(7, request.TimeoutSeconds);
            Assert.AreEqual("Unity", request.Headers["X-Client"]);
            Assert.AreEqual("users", request.QueryParameters["include"]);
        }

        [Test]
        public void EndpointDefinition_ValidationRejectsUnsafeDefinitions()
        {
            ApiEndpointDefinition empty = ScriptableObject.CreateInstance<ApiEndpointDefinition>();
            Assert.IsFalse(empty.IsValid(out string emptyMessage));
            Assert.That(emptyMessage, Does.Contain("path"));

            ApiEndpointDefinition unresolved = ScriptableObject.CreateInstance<ApiEndpointDefinition>();
            unresolved.Path = "projects/{id}";
            Assert.IsFalse(unresolved.IsValid(out string parameterMessage));
            Assert.That(parameterMessage, Does.Contain("{id}"));
        }

        [Test]
        public void ApiClientExtension_SendsEndpointDefinition()
        {
            RecordingApiClient client = new RecordingApiClient();
            ApiEndpointDefinition endpoint = ScriptableObject.CreateInstance<ApiEndpointDefinition>();
            endpoint.Path = "projects";
            endpoint.Method = HttpMethod.POST;
            endpoint.Authentication = ApiAuthenticationRequirement.Required;

            client.SendAsync<string>(endpoint, new { name = "Project" }, CancellationToken.None)
                  .GetAwaiter()
                  .GetResult();

            Assert.AreEqual("projects", client.LastEndpoint.Path);
            Assert.AreEqual(HttpMethod.POST, client.LastEndpoint.Method);
            Assert.AreEqual(ApiAuthenticationRequirement.Required, client.LastEndpoint.Authentication);
            Assert.NotNull(client.LastBody);
        }

        [Test]
        public void ApiClient_ReturnsStringResponsesAsText()
        {
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder, new SuccessRequestSender("healthy"));

            ApiResult<string> result = client.GetAsync<string>("health", CancellationToken.None)
                                             .GetAwaiter()
                                             .GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("healthy", result.Data);
            Assert.AreEqual(ApiResponseFormat.Text, builder.LastResponseFormat);
        }

        [Test]
        public void ApiClient_ReturnsByteArrayResponsesAsBytes()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder, new SuccessRequestSender(null, bytes));

            ApiResult<byte[]> result = client.GetAsync<byte[]>("files/report", CancellationToken.None)
                                             .GetAwaiter()
                                             .GetResult();

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(bytes, result.Data);
            Assert.AreEqual(ApiResponseFormat.Bytes, builder.LastResponseFormat);
        }

        [Test]
        public void ApiClient_ReturnsTextureResponsesAsTextures()
        {
            Texture2D texture = new Texture2D(1, 1);
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder,
                                                new SuccessRequestSender(null,
                                                                         new byte[] { 1 },
                                                                         texture));

            ApiResult<Texture2D> result = client.GetAsync<Texture2D>("images/avatar", CancellationToken.None)
                                                .GetAwaiter()
                                                .GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(texture, result.Data);
            Assert.AreEqual(ApiResponseFormat.Texture, builder.LastResponseFormat);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void ApiClient_AutoMapsAssetBundleResponseType()
        {
            RecordingRequestBuilder builder = new RecordingRequestBuilder();
            ApiClient client = CreateTestClient(builder, new SuccessRequestSender(null));

            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "AssetBundle response could not be decoded for " +
                    "https://example\\.com/bundles/model\\."));
            ApiResult<AssetBundle> result =
                    client.GetAsync<AssetBundle>("bundles/model", CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();

            Assert.IsFalse(result.IsSuccess);
            Assert.That(result.Error.Message, Does.Contain("AssetBundle response could not be decoded"));
            Assert.AreEqual(ApiResponseFormat.AssetBundle, builder.LastResponseFormat);
        }

        [Test]
        public void ApiClient_SpecialResponseTypesIgnoreGlobalDefaultResponseFormat()
        {
            RecordingRequestBuilder textBuilder = new RecordingRequestBuilder();
            ApiClient textClient = CreateTestClient(textBuilder,
                                                    new SuccessRequestSender("healthy"),
                                                    ApiResponseFormat.Bytes);

            textClient.GetAsync<string>("health", CancellationToken.None)
                      .GetAwaiter()
                      .GetResult();

            Assert.AreEqual(ApiResponseFormat.Text, textBuilder.LastResponseFormat);

            byte[] bytes = new byte[] { 1, 2, 3 };
            RecordingRequestBuilder byteBuilder = new RecordingRequestBuilder();
            ApiClient byteClient = CreateTestClient(byteBuilder,
                                                    new SuccessRequestSender(null, bytes),
                                                    ApiResponseFormat.Text);

            byteClient.GetAsync<byte[]>("files/report", CancellationToken.None)
                      .GetAwaiter()
                      .GetResult();

            Assert.AreEqual(ApiResponseFormat.Bytes, byteBuilder.LastResponseFormat);
        }

        [Test]
        public void ResponseParser_FailsClearlyWhenTextureCannotDecode()
        {
            ApiResponseParser parser = new ApiResponseParser(new NewtonsoftApiSerializer());
            ApiRequest request = new ApiRequest("images/avatar", HttpMethod.GET);
            ApiTransportResponse response = new ApiTransportResponse
            {
                    StatusCode = 200,
                    RequestUrl = "https://example.com/images/avatar",
                    RawBytes = new byte[] { 1, 2, 3 },
                    Texture = null,
                    UnityResult = UnityWebRequest.Result.Success
            };

            InvalidOperationException ex =
                    Assert.Throws<InvalidOperationException>(() =>
                            parser.Parse<Texture2D>(request, response, ApiResponseFormat.Texture));

            Assert.That(ex.Message, Does.Contain("Texture response could not be decoded"));
            Assert.That(ex.Message, Does.Contain("https://example.com/images/avatar"));
        }

        [Test]
        public void ResponseParser_RejectsAssetBundleFormatForIncompatibleType()
        {
            ApiResponseParser parser = new ApiResponseParser(new NewtonsoftApiSerializer());
            ApiRequest request = new ApiRequest("bundles/model", HttpMethod.GET);
            ApiTransportResponse response = new ApiTransportResponse
            {
                    StatusCode = 200,
                    RequestUrl = "https://example.com/bundles/model",
                    UnityResult = UnityWebRequest.Result.Success
            };

            InvalidOperationException ex =
                    Assert.Throws<InvalidOperationException>(() =>
                            parser.Parse<byte[]>(request, response, ApiResponseFormat.AssetBundle));

            Assert.That(ex.Message, Does.Contain("requires TResponse AssetBundle"));
        }

        [Test]
        public void ResponseParser_FailsClearlyWhenAssetBundleCannotDecode()
        {
            ApiResponseParser parser = new ApiResponseParser(new NewtonsoftApiSerializer());
            ApiRequest request = new ApiRequest("bundles/model", HttpMethod.GET);
            ApiTransportResponse response = new ApiTransportResponse
            {
                    StatusCode = 200,
                    RequestUrl = "https://example.com/bundles/model",
                    UnityResult = UnityWebRequest.Result.Success
            };

            InvalidOperationException ex =
                    Assert.Throws<InvalidOperationException>(() =>
                            parser.Parse<AssetBundle>(request, response, ApiResponseFormat.AssetBundle));

            Assert.That(ex.Message, Does.Contain("AssetBundle response could not be decoded"));
            Assert.That(ex.Message, Does.Contain("https://example.com/bundles/model"));
        }

        [TestCase(ApiResponseFormat.Json, "application/json")]
        [TestCase(ApiResponseFormat.Text, "text/plain,*/*")]
        [TestCase(ApiResponseFormat.Bytes, "*/*")]
        [TestCase(ApiResponseFormat.Texture, "image/png,image/jpeg,image/webp,*/*")]
        [TestCase(ApiResponseFormat.AssetBundle, "application/vnd.unity.assetbundle,application/octet-stream,*/*")]
        public void RequestBuilder_AppliesAcceptHeaderDefaults(ApiResponseFormat responseFormat,
                                                               string expectedAccept)
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("projects", HttpMethod.GET);

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, responseFormat, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual(expectedAccept, webRequest.GetRequestHeader("Accept"));
            }
        }

        [Test]
        public void RequestBuilder_UsesAssetBundleDownloadHandler()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("bundles/model", HttpMethod.GET)
            {
                    ResponseFormat = ApiResponseFormat.AssetBundle,
                    TimeoutSeconds = 9,
                    AssetBundleOptions = new ApiAssetBundleRequestOptions
                    {
                            CacheMode = ApiAssetBundleCacheMode.UseUnityCache,
                            CacheKey = "model-bundle",
                            CacheHash = "0123456789abcdef0123456789abcdef",
                            Crc = 123
                    }
            };
            request.Headers["X-Trace"] = "abc";

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.AssetBundle, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("GET", webRequest.method);
                Assert.IsInstanceOf<DownloadHandlerAssetBundle>(webRequest.downloadHandler);
                Assert.AreEqual(9, webRequest.timeout);
                Assert.AreEqual("abc", webRequest.GetRequestHeader("X-Trace"));
                Assert.AreEqual("application/vnd.unity.assetbundle,application/octet-stream,*/*",
                                webRequest.GetRequestHeader("Accept"));
            }
        }

        [Test]
        public void RequestBuilder_AcceptsAssetBundleCacheVariants()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();

            ApiAssetBundleRequestOptions[] options =
            {
                    new ApiAssetBundleRequestOptions { CacheMode = ApiAssetBundleCacheMode.Disabled, Crc = 1 },
                    new ApiAssetBundleRequestOptions
                    {
                            CacheMode = ApiAssetBundleCacheMode.UseUnityCache,
                            CacheHash = "0123456789abcdef0123456789abcdef",
                            Crc = 2
                    },
                    new ApiAssetBundleRequestOptions
                    {
                            CacheMode = ApiAssetBundleCacheMode.UseUnityCache,
                            CacheVersion = 7,
                            Crc = 3
                    }
            };

            foreach (ApiAssetBundleRequestOptions option in options)
            {
                ApiRequest request = new ApiRequest("bundles/model", HttpMethod.GET)
                {
                        AssetBundleOptions = option
                };

                using (UnityWebRequest webRequest =
                       builder.BuildAsync(request, ApiResponseFormat.AssetBundle, CancellationToken.None)
                              .GetAwaiter()
                              .GetResult())
                {
                    Assert.IsInstanceOf<DownloadHandlerAssetBundle>(webRequest.downloadHandler);
                }
            }
        }

        [Test]
        public void RequestBuilder_RejectsNonGetAssetBundleRequests()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("bundles/model", HttpMethod.POST)
            {
                    Body = new { name = "model" }
            };

            InvalidOperationException ex =
                    Assert.Throws<InvalidOperationException>(() =>
                            builder.BuildAsync(request, ApiResponseFormat.AssetBundle, CancellationToken.None)
                                   .GetAwaiter()
                                   .GetResult());

            Assert.That(ex.Message, Does.Contain("GET"));
        }

        [Test]
        public void TransferProgress_ClampsProgressAndBytes()
        {
            ApiTransferProgress progress = ApiTransferProgress.Create(1.5f, -1f, -42, 128, true);

            Assert.AreEqual(1f, progress.DownloadProgress);
            Assert.AreEqual(0f, progress.UploadProgress);
            Assert.AreEqual(0, progress.DownloadedBytes);
            Assert.AreEqual(128, progress.UploadedBytes);
            Assert.IsTrue(progress.IsDone);
        }

        [Test]
        public void RequestBuilder_PreservesExplicitAcceptHeader()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("projects", HttpMethod.GET);
            request.Headers["Accept"] = "application/pdf";

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Bytes, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("application/pdf", webRequest.GetRequestHeader("Accept"));
            }
        }

        [Test]
        public void RequestBuilder_PreservesExplicitContentTypeHeader()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("projects", HttpMethod.POST)
            {
                    Body = "hello",
                    BodyFormat = ApiRequestBodyFormat.RawText
            };
            request.Headers["Content-Type"] = "text/csv";

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Text, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("text/csv", webRequest.GetRequestHeader("Content-Type"));
            }
        }

        [Test]
        public void RequestBuilder_CreatesRawTextRequestBody()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("echo", HttpMethod.POST)
            {
                    Body = "hello",
                    BodyFormat = ApiRequestBodyFormat.RawText
            };

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Text, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("text/plain", webRequest.GetRequestHeader("Content-Type"));
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hello"), webRequest.uploadHandler.data);
            }
        }

        [Test]
        public void RequestBuilder_CreatesRawByteRequestBody()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            byte[] body = new byte[] { 10, 20, 30 };
            ApiRequest request = new ApiRequest("upload", HttpMethod.POST)
            {
                    Body = body,
                    BodyFormat = ApiRequestBodyFormat.RawBytes
            };

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Bytes, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("application/octet-stream", webRequest.GetRequestHeader("Content-Type"));
                CollectionAssert.AreEqual(body, webRequest.uploadHandler.data);
            }
        }

        [Test]
        public void RequestBuilder_CreatesMultipartRequestBody()
        {
            UnityWebRequestBuilder builder = CreateRequestBuilder();
            ApiRequest request = new ApiRequest("upload", HttpMethod.POST)
            {
                    Body = new List<IMultipartFormSection>
                    {
                            new MultipartFormDataSection("name", "value")
                    },
                    BodyFormat = ApiRequestBodyFormat.MultipartForm
            };

            using (UnityWebRequest webRequest =
                   builder.BuildAsync(request, ApiResponseFormat.Json, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult())
            {
                Assert.AreEqual("POST", webRequest.method);
                Assert.NotNull(webRequest.uploadHandler);
                Assert.That(webRequest.uploadHandler.contentType, Does.Contain("multipart/form-data"));
            }
        }

        [Test]
        public void LegacyDownloadBytesWrapper_DelegatesToConfiguredApiClient()
        {
            IApiClient original = ApiServices.Client;
            RecordingApiClient recordingClient = new RecordingApiClient();

            try
            {
                ApiServices.Configure(recordingClient);

#pragma warning disable CS0618
                ApiServices.DownloadBytesAsync("files/report", "token", null, CancellationToken.None)
                           .GetAwaiter()
                           .GetResult();
#pragma warning restore CS0618

                Assert.AreEqual("files/report", recordingClient.LastRequest.Endpoint);
                Assert.AreEqual(ApiResponseFormat.Bytes, recordingClient.LastRequest.ResponseFormat);
                Assert.AreEqual("token", recordingClient.LastRequest.BearerTokenOverride);
            }
            finally
            {
                ApiServices.Configure(original);
            }
        }

        [Test]
        public void AssemblyDefinitions_ArePackageSafe()
        {
            string apiRoot = FindPackageRoot();

            string runtime = ReadAsmdef(Path.Combine(apiRoot, "Runtime", "Deucarian.API.asmdef"));
            string editor = ReadAsmdef(Path.Combine(apiRoot, "Editor", "Deucarian.API.Editor.asmdef"));
            string tests = ReadAsmdef(Path.Combine(apiRoot, "Tests", "Editor", "Deucarian.API.Tests.asmdef"));
            string samples = ReadAsmdef(Path.Combine(apiRoot,
                                                     "Samples~",
                                                     "ExampleScene",
                                                     "Deucarian.API.Samples.asmdef"));

            Assert.That(runtime, Does.Contain("\"includePlatforms\": []"));
            Assert.That(runtime, Does.Not.Contain("\"CertificateAssembly\""));
            Assert.That(runtime, Does.Not.Contain("GUID:"));
            Assert.That(runtime, Does.Not.Contain("\"Editor\""));
            Assert.That(editor, Does.Contain("\"includePlatforms\": ["));
            Assert.That(editor, Does.Contain("\"Editor\""));
            Assert.That(editor, Does.Contain("\"Deucarian.API\""));
            Assert.That(editor, Does.Contain("\"Deucarian.Editor\""));
            Assert.That(editor, Does.Not.Contain("GUID:"));
            Assert.That(tests, Does.Contain("\"includePlatforms\": ["));
            Assert.That(tests, Does.Contain("\"Editor\""));
            Assert.That(tests, Does.Contain("\"Deucarian.API\""));
            Assert.That(tests, Does.Contain("\"TestAssemblies\""));
            Assert.That(tests, Does.Not.Contain("GUID:"));
            Assert.That(samples, Does.Contain("\"references\": ["));
            Assert.That(samples, Does.Contain("\"Deucarian.API\""));
            Assert.That(samples, Does.Not.Contain("GUID:"));
            Assert.That(samples, Does.Not.Contain("\"Deucarian.API.Editor\""));
        }

        [Test]
        public void PackageMetadata_DeclaresSharedEditorMenuDependency()
        {
            string apiRoot = FindPackageRoot();
            string packageManifest = File.ReadAllText(Path.Combine(apiRoot, "package.json"));
            string packageConfig = File.ReadAllText(Path.Combine(apiRoot, "deucarian-package.json"));

            Assert.That(packageManifest, Does.Contain("\"com.deucarian.editor\""));
            Assert.That(packageConfig, Does.Contain("\"com.deucarian.editor\""));
        }

        [Test]
        public void ApiServices_FacadeDelegatesEndpointCallsToConfiguredClient()
        {
            IApiClient original = ApiServices.Client;
            RecordingApiClient recordingClient = new RecordingApiClient();
            ApiEndpoint endpoint = new ApiEndpoint("projects", HttpMethod.GET);

            try
            {
                ApiServices.Configure(recordingClient);

                ApiServices.SendAsync<string>(endpoint, CancellationToken.None)
                           .GetAwaiter()
                           .GetResult();

                Assert.AreSame(endpoint, recordingClient.LastEndpoint);
            }
            finally
            {
                ApiServices.Configure(original);
            }
        }

        private static ApiClient CreateTestClient(IRequestBuilder builder,
                                                  IRequestSender sender,
                                                  ApiResponseFormat defaultResponseFormat = ApiResponseFormat.Auto,
                                                  ApiClientConfig config = null)
        {
            return new ApiClient(builder,
                                 sender,
                                 new ApiResponseParser(new NewtonsoftApiSerializer()),
                                 new ApiErrorParser(),
                                 config ?? ApiClientConfig.CreateRuntimeDefault(),
                                 defaultResponseFormat);
        }

        private static UnityWebRequestBuilder CreateRequestBuilder()
        {
            ApiClientConfig config = ScriptableObject.CreateInstance<ApiClientConfig>();
            config.BaseUrl = "https://example.com";

            return new UnityWebRequestBuilder(
                    config,
                    new NewtonsoftApiSerializer(),
                    null,
                    new ApiCertificateHandlerFactory(ApiCertificateHandlingMode.DefaultValidation));
        }

        private static string ReadAsmdef(string path)
        {
            return File.ReadAllText(path);
        }

        private static string FindPackageRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] candidates =
            {
                    Path.Combine(projectRoot, "Packages", "com.deucarian.api"),
                    Path.Combine(projectRoot, "Packages", "API"),
                    Path.Combine(Application.dataPath, "Plugins", "Deucarian", "API"),
                    projectRoot
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "package.json"))
                    && File.Exists(Path.Combine(candidate, "Runtime", "Deucarian.API.asmdef")))
                {
                    return candidate;
                }

                if (File.Exists(Path.Combine(candidate, "Deucarian.API.asmdef"))
                    && Directory.Exists(Path.Combine(candidate, "Core")))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException("Could not locate the API package root.");
        }

        private sealed class RecordingRequestBuilder : IRequestBuilder
        {
            public ApiRequest LastRequest { get; private set; }
            public ApiResponseFormat LastResponseFormat { get; private set; }

            public Task<UnityWebRequest> BuildAsync(ApiRequest request,
                                                    ApiResponseFormat responseFormat,
                                                    CancellationToken cancellationToken)
            {
                LastRequest = request;
                LastResponseFormat = responseFormat;
                return Task.FromResult(new UnityWebRequest("https://example.com/" + request.Endpoint,
                                                           request.Method.ToString()));
            }
        }

        private sealed class SuccessRequestSender : IRequestSender
        {
            private readonly string _body;
            private readonly byte[] _rawBytes;
            private readonly Texture2D _texture;

            public SuccessRequestSender(string body, byte[] rawBytes = null, Texture2D texture = null)
            {
                _body = body;
                _rawBytes = rawBytes;
                _texture = texture;
            }

            public Task<ApiTransportResponse> SendAsync(UnityWebRequest request,
                                                        ApiRequest apiRequest,
                                                        CancellationToken cancellationToken)
            {
                return Task.FromResult(new ApiTransportResponse
                {
                        StatusCode = 200,
                        RequestUrl = request.url,
                        RawBody = _body,
                        RawBytes = _rawBytes ?? (_body == null ? null : Encoding.UTF8.GetBytes(_body)),
                        Texture = _texture,
                        UnityResult = UnityWebRequest.Result.Success
                });
            }
        }

        private sealed class CancellingRequestSender : IRequestSender
        {
            public Task<ApiTransportResponse> SendAsync(UnityWebRequest request,
                                                        ApiRequest apiRequest,
                                                        CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new ApiTransportResponse());
            }
        }

        private sealed class FailureRequestSender : IRequestSender
        {
            private readonly long statusCode;
            private readonly string rawBody;

            internal FailureRequestSender(long statusCode, string rawBody)
            {
                this.statusCode = statusCode;
                this.rawBody = rawBody;
            }

            public Task<ApiTransportResponse> SendAsync(
                UnityWebRequest request,
                ApiRequest apiRequest,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new ApiTransportResponse
                {
                    StatusCode = statusCode,
                    RequestUrl = request.url,
                    RawBody = rawBody,
                    RawBytes = Encoding.UTF8.GetBytes(rawBody),
                    UnityResult = UnityWebRequest.Result.ProtocolError
                });
            }
        }

        private sealed class RecordingApiClient : IApiClient
        {
            public ApiEndpoint LastEndpoint { get; private set; }
            public ApiRequest LastRequest { get; private set; }
            public object LastBody { get; private set; }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(ApiRequest request,
                                                                   CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                LastBody = request.Body;
                return Success<TResponse>(request.Method, request.Endpoint);
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(ApiEndpoint endpoint,
                                                                   CancellationToken cancellationToken = default)
            {
                LastEndpoint = endpoint;
                return Success<TResponse>(endpoint.Method, endpoint.Path);
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(ApiEndpoint endpoint,
                                                                   object body,
                                                                   CancellationToken cancellationToken = default)
            {
                LastEndpoint = endpoint;
                LastBody = body;
                return Success<TResponse>(endpoint.Method, endpoint.Path);
            }

            public Task<ApiResult<TResponse>> GetAsync<TResponse>(string endpoint,
                                                                  CancellationToken cancellationToken = default)
            {
                return Success<TResponse>(HttpMethod.GET, endpoint);
            }

            public Task<ApiResult<TResponse>> PostAsync<TResponse>(string endpoint,
                                                                   object body,
                                                                   CancellationToken cancellationToken = default)
            {
                LastBody = body;
                return Success<TResponse>(HttpMethod.POST, endpoint);
            }

            public Task<ApiResult<TResponse>> PutAsync<TResponse>(string endpoint,
                                                                  object body,
                                                                  CancellationToken cancellationToken = default)
            {
                LastBody = body;
                return Success<TResponse>(HttpMethod.PUT, endpoint);
            }

            public Task<ApiResult<TResponse>> PatchAsync<TResponse>(string endpoint,
                                                                    object body,
                                                                    CancellationToken cancellationToken = default)
            {
                LastBody = body;
                return Success<TResponse>(HttpMethod.PATCH, endpoint);
            }

            public Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string endpoint,
                                                                     CancellationToken cancellationToken = default)
            {
                return Success<TResponse>(HttpMethod.DELETE, endpoint);
            }

            private static Task<ApiResult<TResponse>> Success<TResponse>(HttpMethod method, string endpoint)
            {
                return Task.FromResult(ApiResult<TResponse>.Success(default(TResponse),
                                                                    method,
                                                                    200,
                                                                    endpoint,
                                                                    null));
            }
        }
    }
}
