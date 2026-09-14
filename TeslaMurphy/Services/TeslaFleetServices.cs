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
        private static readonly SemaphoreSlim tokenLock = new SemaphoreSlim(1, 1);

        // Persist the rotated refresh token together with the new access-token lifetime.
        public static bool SaveSession(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var token = Newtonsoft.Json.Linq.JObject.Parse(json);
            string access = (string)token["access_token"];
            if (string.IsNullOrWhiteSpace(access)) return false;
            string refresh = (string)token["refresh_token"];
            long seconds = (long?)token["expires_in"] ?? 0;
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                values["refreshtoken"] = refresh;
                AppSettings.Instance.Refresh_token = refresh;
            }
            values["accesstoken"] = access;
            values["accessTokenExpiresUtc"] = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, seconds)).ToUnixTimeSeconds();
            AppSettings.Instance.Access_token = access;
            return true;
        }

        public static async Task<bool> EnsureSessionAsync(string rejectedToken = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await tokenLock.WaitAsync(cancellationToken);
            try
            {
                var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                string access = values.ContainsKey("accesstoken") ? values["accesstoken"] as string : null;
                if (string.IsNullOrWhiteSpace(access)) return false;
                AppSettings.Instance.Access_token = access;
                // Another request may already have renewed the rejected token.
                if (rejectedToken != null && access != rejectedToken) return true;
                long expires;
                if (rejectedToken == null && values.ContainsKey("accessTokenExpiresUtc")
                    && long.TryParse(values["accessTokenExpiresUtc"].ToString(), out expires)
                    && expires > DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds()) return true;
                string refresh = values.ContainsKey("refreshtoken") ? values["refreshtoken"] as string : null;
                if (string.IsNullOrWhiteSpace(refresh)) return false;
                AppSettings.Instance.Refresh_token = refresh;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    string result = await RefreshTokenRequestAsync(cts);
                    // Do not restore a session after logout or overwrite a newer login.
                    if (!values.ContainsKey("refreshtoken") || values["refreshtoken"] as string != refresh)
                        return false;
                    return SaveSession(result);
                }
            }
            catch (Newtonsoft.Json.JsonException) { return false; }
            finally { tokenLock.Release(); }
        }

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
