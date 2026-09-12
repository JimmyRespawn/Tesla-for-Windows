using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeslaMurphy.Models;

namespace TeslaMurphy.Services
{
    internal static class TeslaFleetServices
    {
        public static Task<string> HttpGetRequestAsync(string base_url, string endpoint, string access_token, CancellationTokenSource cts)
            => HttpService.SendFleetAsync(HttpMethod.Get, base_url, endpoint, access_token, cts?.Token ?? CancellationToken.None);

        public static Task<string> HttpPostRequestAsync(string base_url, string endpoint, string access_token, CancellationTokenSource cts)
            => HttpService.SendFleetAsync(HttpMethod.Post, base_url, endpoint, access_token, cts?.Token ?? CancellationToken.None);

        public static Task<string> GenerateAuthorizeUriAsync()
        {
            string url = TeslaConfiguration.AuthorizeUrl
                + "?client_id=" + Uri.EscapeDataString(AppSettings.Instance.Client_id)
                + "&prompt=login&redirect_uri=" + Uri.EscapeDataString(TeslaConfiguration.RedirectUri)
                + "&response_type=code&scope=" + Uri.EscapeDataString(TeslaConfiguration.Scope)
                + "&state=" + Guid.NewGuid().ToString();
            return Task.FromResult(url);
        }

        public static Task<string> ExchangeCodeAsync(string code)
        {
            var settings = AppSettings.Instance;
            return HttpService.PostFormAsync(TeslaConfiguration.TokenUrl, new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", TeslaConfiguration.RedirectUri),
                new KeyValuePair<string, string>("client_id", settings.Client_id),
                new KeyValuePair<string, string>("client_secret", settings.Client_secret),
                new KeyValuePair<string, string>("audience", settings.Base_URL),
                new KeyValuePair<string, string>("scope", TeslaConfiguration.Scope),
                new KeyValuePair<string, string>("code", code)
            }, CancellationToken.None);
        }

        public static async Task<string> RefreshTokenRequestAsync(CancellationTokenSource cts)
        {
            try
            {
                return await HttpService.PostFormAsync(TeslaConfiguration.TokenUrl, new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", AppSettings.Instance.Client_id),
                    new KeyValuePair<string, string>("refresh_token", AppSettings.Instance.Refresh_token)
                }, cts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { return null; }
            catch (HttpRequestException) { return null; }
        }
    }
}
