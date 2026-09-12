using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TeslaMurphy.Services
{
    internal static class HttpService
    {
        private static readonly HttpClient client = new HttpClient();

        internal sealed class CommandResponse
        {
            public int StatusCode { get; set; }
            public string Body { get; set; }
            public string TransportError { get; set; }
        }

        public static async Task<CommandResponse> SendCommandAsync(string baseUrl, string endpoint,
            string accessToken, CancellationToken cancellationToken, string jsonBody = "{}")
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + endpoint))
                {
                    request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var response = await client.SendAsync(request, cancellationToken))
                        return new CommandResponse
                        {
                            StatusCode = (int)response.StatusCode,
                            Body = await response.Content.ReadAsStringAsync()
                        };
                }
            }
            catch (OperationCanceledException)
            {
                return new CommandResponse { TransportError = "Request timed out or was canceled. Check that the vehicle is online." };
            }
            catch (HttpRequestException)
            {
                return new CommandResponse { TransportError = "Unable to reach the server. Check the network, proxy address and HTTPS certificate." };
            }
        }

        // Preserve the status-name/null contract consumed by existing vehicle views.
        public static async Task<string> SendFleetAsync(HttpMethod method, string baseUrl,
            string endpoint, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(method, baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/')))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var response = await client.SendAsync(request, cancellationToken))
                        return response.IsSuccessStatusCode
                            ? await response.Content.ReadAsStringAsync()
                            : response.StatusCode.ToString();
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (HttpRequestException) { return null; }
        }

        public static async Task<string> PostFormAsync(string url,
            IEnumerable<KeyValuePair<string, string>> fields, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new FormUrlEncodedContent(fields);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using (var response = await client.SendAsync(request, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
