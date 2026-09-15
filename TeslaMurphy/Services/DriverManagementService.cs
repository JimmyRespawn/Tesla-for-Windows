using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;

namespace TeslaMurphy.Services
{
    internal sealed class DriverManagementService
    {
        private readonly string baseUrl;
        private readonly string vehiclePath;
        public DriverManagementService(string vin)
        {
            baseUrl = AppSettings.Instance.Base_URL;
            vehiclePath = "/api/1/vehicles/" + Uri.EscapeDataString(vin ?? "");
        }
        public Task<JObject> Drivers() => Send(HttpMethod.Get, vehiclePath + "/drivers");
        public Task<JObject> Invitations(int page) => Send(HttpMethod.Get,
            vehiclePath + "/invitations?page=" + page + "&per_page=25");
        public Task<JObject> Create() => Send(HttpMethod.Post, vehiclePath + "/invitations", new JObject());
        public Task<JObject> Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Missing driver identifier.");
            return Send(HttpMethod.Delete, vehiclePath + "/drivers?share_user_id=" + Uri.EscapeDataString(id));
        }
        public Task<JObject> Revoke(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Missing invitation identifier.");
            return Send(HttpMethod.Post, vehiclePath + "/invitations/" + Uri.EscapeDataString(id) + "/revoke", new JObject());
        }
        public Task<JObject> Redeem(string input) => Send(HttpMethod.Post,
            "/api/1/invitations/redeem", new JObject { ["code"] = InvitationCode(input) });

        internal static string InvitationCode(string input)
        {
            string code = (input ?? "").Trim();
            if (Uri.TryCreate(code, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme != "https" || !(uri.Host == "tesla.com" || uri.Host == "www.tesla.com"
                    || uri.Host == "tesla.cn" || uri.Host == "www.tesla.cn")
                    || !uri.AbsolutePath.StartsWith("/_rs/1/", StringComparison.Ordinal))
                    throw new ArgumentException("Enter a Tesla invitation link or invitation code.");
                code = Uri.UnescapeDataString(uri.AbsolutePath.Substring(7)).TrimEnd('/');
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_-]{16,256}$"))
                throw new ArgumentException("Invalid invitation code.");
            return code;
        }

        internal static JObject Parse(HttpService.CommandResponse response)
        {
            if (response.TransportError != null) throw new InvalidOperationException(response.TransportError + " Refresh before retrying.");
            if (response.StatusCode == 401) throw new InvalidOperationException("Session expired. Please sign in again.");
            if (response.StatusCode == 403) throw new InvalidOperationException("Access denied. Owner access and the required permissions are needed.");
            if (response.StatusCode < 200 || response.StatusCode >= 300)
                throw new InvalidOperationException("Tesla returned HTTP " + response.StatusCode + ". Refresh to check the result before retrying.");
            if (response.StatusCode == 204) return new JObject { ["response"] = true };
            JObject json;
            try { json = JObject.Parse(response.Body); }
            catch { throw new InvalidOperationException("Unexpected Tesla response. Refresh to check the result."); }
            var result = json["response"];
            if (!string.IsNullOrEmpty((string)json["error"]) || result == null || result.Type == JTokenType.Null
                || (result.Type == JTokenType.Boolean && !(bool)result)
                || (result is JObject obj && obj["result"]?.Type == JTokenType.Boolean && !(bool)obj["result"]))
                throw new InvalidOperationException("Tesla did not complete the request. Refresh and check your permissions.");
            return json;
        }
        private async Task<JObject> Send(HttpMethod method, string endpoint, JObject body = null)
        {
            if (AppSettings.Instance.IsTestMode) throw new InvalidOperationException("Driver management is unavailable in demo mode.");
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                return Parse(await HttpService.SendManagementAsync(method, baseUrl, endpoint, cts.Token, body?.ToString()));
        }
    }
}
