﻿using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;

namespace Axe.SimpleHttpMock
{
    /// <summary>
    /// An extension class which ease the way to create a stub-response.
    /// </summary>
    public static class ResponseExtension
    {
        /// <summary>
        /// Create a response from HTTP status code.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns>An HTTP response message.</returns>
        public static HttpResponseMessage AsResponse(this HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode);
        }

        /// <summary>
        /// Create a response from a payload object and HTTP status code.
        /// </summary>
        /// <param name="payload">
        /// The content object of the response. Please not that if this argument is <c>null</c>, 
        /// no content will be specified.
        /// </param>
        /// <param name="statusCode">The status code of the response, default is <see cref="HttpStatusCode.OK"/>.</param>
        /// <param name="formatter">The content formatter.</param>
        /// <returns>An HTTP response message.</returns>
        public static HttpResponseMessage AsResponse(
            this object payload, 
            HttpStatusCode statusCode = HttpStatusCode.OK,
            MediaTypeFormatter formatter = null)
        {
            ObjectContent content = payload == null
                ? null
                : new ObjectContent(payload.GetType(), payload, formatter ?? new JsonMediaTypeFormatter());
            return new HttpResponseMessage(statusCode)
            {
                Content = content
            };
        }

        /// <summary>
        /// Create a response directly form an HTTP content.
        /// </summary>
        /// <param name="content">The content</param>
        /// <param name="statusCode">The status code of the response.</param>
        /// <returns>An HTTP response message.</returns>
        public static HttpResponseMessage AsResponse(
            this HttpContent content,
            HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = content
            };
        }
    }
}