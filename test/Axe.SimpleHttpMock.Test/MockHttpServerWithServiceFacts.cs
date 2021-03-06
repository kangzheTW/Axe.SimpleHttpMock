﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Axe.SimpleHttpMock.Test.Helpers;
using Newtonsoft.Json;
using Xunit;

namespace Axe.SimpleHttpMock.Test
{
    public class MockHttpServerWithServiceFacts : HttpServerFactBase
    {
        [Theory]
        [InlineData("http://www.base.com", "", "http://www.base.com")]
        [InlineData("http://www.base.com/", "", "http://www.base.com")]
        [InlineData("http://www.base.com", "", "http://www.base.com/")]
        [InlineData("http://www.base.com", "user", "http://www.base.com/user")]
        [InlineData("http://www.base.com", "user", "http://www.base.com/user/")]
        [InlineData("http://www.base.com", "user", "http://www.base.com/user?query=string")]
        [InlineData("http://www.base.com", "user/", "http://www.base.com/user/?query=string")]
        [InlineData("http://www.base.com", "user/{userId}", "http://www.base.com/user/1.htm?query=string")]
        [InlineData("http://www.base.com", "user/{userId}", "http://www.base.com/user/1.htm/?query=string")]
        [InlineData("http://www.base.com", "user/{userId}/session", "http://www.base.com/user/1.htm/session?query=string")]
        [InlineData("http://www.base.com", "user?query=string", "http://www.base.com/user?query=string")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user?query=whatever")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user?query=")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user?query=&other=value")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user?other=value&query=")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user?other=value")]
        [InlineData("http://www.base.com", "user?query={value}", "http://www.base.com/user/?query=value")]
        [InlineData("http://www.base.com", "user", "http://www.base.com/user/?query=string")]
        [InlineData("http://www.base.com", "user/", "http://www.base.com/user?query=string")]
        public async Task should_return_desired_message_if_handler_matches_for_uri(
            string serviceUri, string route, string requestUri)
        {
            var server = new MockHttpServer();
            var content = new { name = "hello" };
            server.WithService(serviceUri).Api(route, "GET", content);

            HttpClient client = CreateClient(server);
            HttpResponseMessage response = await client.GetAsync(requestUri);

            Assert.Equal(response.StatusCode, HttpStatusCode.OK);
            var actualContent = JsonConvert.DeserializeAnonymousType(
                await response.Content.ReadAsStringAsync(),
                content);
            Assert.Equal("hello", actualContent.name);
        }

        [Theory]
        [InlineData("http://www.base.com", "", "http://www.base.com/user")]
        [InlineData("http://www.base.com", "user/{userId}/session", "http://www.base.com/user/1/2/session?query=string")]
        [InlineData("http://www.base.com", "user?query=string", "http://www.base.com/user?parameter=string")]
        public async Task should_return_404_if_no_handler_matches_for_uri(
            string serviceUri, string template, string requestUri)
        {
            var server = new MockHttpServer();
            server.WithService(serviceUri).Api(template, "GET", HttpStatusCode.OK);

            HttpClient client = CreateClient(server);
            HttpResponseMessage response = await client.GetAsync(requestUri);

            Assert.Equal(response.StatusCode, HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("", "GET")]
        [InlineData("", "PUT")]
        [InlineData("", "POST")]
        [InlineData("", "DELETE")]
        [InlineData("GET", "GET")]
        [InlineData("GET,PUT", "GET")]
        [InlineData("GET,PUT", "PUT")]
        public async Task should_return_desired_message_if_http_method_is_matched(
            string httpMethodConfig, string actualMethod)
        {
            string[] httpMethods = httpMethodConfig.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (httpMethods.Length == 0) { httpMethods = null; }

            var server = new MockHttpServer();
            server.WithService("http://www.base.com").Api("user", httpMethods, HttpStatusCode.OK);

            HttpRequestMessage request = new HttpRequestMessage(
                new HttpMethod(actualMethod), "http://www.base.com/user");
            HttpClient client = CreateClient(server);
            HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(response.StatusCode, HttpStatusCode.OK);
        }

        [Fact]
        public async Task should_not_confuse_between_different_systems()
        {
            var server = new MockHttpServer();

            server
                .WithService("http://service1.com")
                .Api("user", new {message = "from service 1"})
                .Done()
                .WithService("http://service2.com")
                .Api("user", new {message = "from service 2"})
                .Done();

            HttpClient client = CreateClient(server);

            HttpResponseMessage responseSvc1 = await client.GetAsync("http://service1.com/user");
            HttpResponseMessage responseSvc2 = await client.GetAsync("http://service2.com/user");
            HttpResponseMessage responseSvc3 = await client.GetAsync("http://service-not-exist.com/user");

            var template = new {message = default(string)};

            Assert.Equal("from service 1", (await responseSvc1.ReadAs(template)).message);
            Assert.Equal("from service 2", (await responseSvc2.ReadAs(template)).message);
            Assert.Equal(HttpStatusCode.NotFound, responseSvc3.StatusCode);
        }

        [Fact]
        public async Task should_handle_by_latest_matched_uri()
        {
            var server = new MockHttpServer();
            server.WithService("http://www.base.com/user")
                .Api("account", HttpStatusCode.OK, "route 1")
                .Api("account", "GET", HttpStatusCode.OK, "route 2");

            HttpClient client = CreateClient(server);
            HttpResponseMessage response = await client.GetAsync("http://www.base.com/user/account");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            server["route 2"].VerifyHasBeenCalled();
            server["route 1"].VerifyNotCalled();
        }

        [Fact]
        public async Task should_get_binding_parameters_of_uri()
        {
            var server = new MockHttpServer();
            server
                .WithService("http://www.base.com")
                .Api(
                    "user/{userId}/session/{sessionId}?p1={value1}&p2={value2}",
                    "GET",
                    _ => new { Parameter = _ }.AsResponse());

            HttpClient client = CreateClient(server);

            HttpResponseMessage response = await client.GetAsync(
                "http://www.base.com/user/12/session/28000?p1=v1&p2=v2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string jsonString = await response.Content.ReadAsStringAsync();
            var content = JsonConvert.DeserializeAnonymousType(
                jsonString,
                new { Parameter = default(Dictionary<string, object>) });

            Assert.Equal("12", content.Parameter["userId"]);
            Assert.Equal("28000", content.Parameter["sessionId"]);
            Assert.Equal("v1", content.Parameter["value1"]);
            Assert.Equal("v2", content.Parameter["value2"]);
        }

        [Fact]
        public async Task should_get_optional_query_parameters_of_uri()
        {
            var server = new MockHttpServer();
            server
                .WithService("http://www.base.com")
                .Api(
                    "user/{userId}/session/{sessionId}?p1={value1}&p2={value2}",
                    "GET",
                    _ => new { Parameter = _ }.AsResponse());

            HttpClient client = CreateClient(server);

            HttpResponseMessage response = await client.GetAsync(
                "http://www.base.com/user/12/session/28000?p2=v2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string jsonString = await response.Content.ReadAsStringAsync();
            var content = JsonConvert.DeserializeAnonymousType(
                jsonString,
                new { Parameter = default(Dictionary<string, object>) });

            Assert.Equal(string.Empty, content.Parameter["value1"]);
            Assert.Equal("v2", content.Parameter["value2"]);
        }

        [Fact]
        public async Task should_get_expected_response_for_different_api_setup_styles()
        {
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) => 
                w.Api(uri, (req, p) => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods.Single(), (req, p) => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods, (req, p) => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, p => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods.Single(), p => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods, p => content.AsResponse(statusCode), name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods.Single(), statusCode, name),
                false);
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods, statusCode, name),
                false);
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, statusCode, name),
                false);
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods.Single(), content, name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, methods, content, name));
            await TestApiFunction(
                (w, uri, methods, content, statusCode, name) =>
                w.Api(uri, content, name));
        }

        [Fact]
        public async Task should_distinguish_sub_applications()
        {
            var server = new MockHttpServer();
            server.WithService("http://domain.com/apps/app1")
                .Api("user", HttpStatusCode.OK);

            server.WithService("http://domain.com/apps/app2")
                .Api("user", HttpStatusCode.Accepted);

            HttpClient client = CreateClient(server);

            HttpResponseMessage responseApp1 = await client.GetAsync("http://domain.com/apps/app1/user");
            HttpResponseMessage responseApp2 = await client.GetAsync("http://domain.com/apps/app2/user");

            Assert.Equal(HttpStatusCode.OK, responseApp1.StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, responseApp2.StatusCode);
        }

        [Fact]
        public async Task should_get_lastest_matched_defintion_even_at_different_level()
        {
            var server = new MockHttpServer();
            server.WithService("http://domain.com/apps/app1")
                .Api("/", HttpStatusCode.BadRequest)
                .Api("user", HttpStatusCode.OK);

            HttpClient client = CreateClient(server);
            HttpResponseMessage responseWithoutOverride = await client.GetAsync("http://domain.com/apps/app1");
            Assert.Equal(HttpStatusCode.BadRequest, responseWithoutOverride.StatusCode);

            server.WithService("http://domain.com/apps")
                .Api("{appName}", HttpStatusCode.Accepted);
            
            HttpResponseMessage response = await client.GetAsync("http://domain.com/apps/app1");

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        delegate void ApiTestSetup(
            WithServiceClause withServiceClause,
            string uriTemplate,
            string[] methods,
            object content,
            HttpStatusCode statusCode,
            string name);

        async Task TestApiFunction(ApiTestSetup setup, bool withContent = true)
        {
            var expectedContent = new {message = "hello"};
            const string handlerName = "handler_name";
            const HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
            const string expectedId = "23421";

            var server = new MockHttpServer();
            WithServiceClause withServiceClause = server.WithService("http://www.base.com/sub");
            setup(withServiceClause, "user?id={id}", new []{"PUT"}, expectedContent, expectedStatusCode, handlerName);

            HttpClient client = CreateClient(server);
            
            HttpResponseMessage response = await client.PutAsJsonAsync($"http://www.base.com/sub/user?id={expectedId}", new object());

            Assert.Equal(expectedStatusCode, response.StatusCode);

            if (withContent)
            {
                string actualMessage = JsonConvert.DeserializeAnonymousType(
                    await response.Content.ReadAsStringAsync(),
                    expectedContent).message;
                Assert.Equal(expectedContent.message, actualMessage);
            }

            server[handlerName].VerifyHasBeenCalled();
            server[handlerName].VerifyHasBeenCalled(1);
            server[handlerName].VerifyBindedParameter("id", expectedId);
        }
    }
}