using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeslaMurphy.Services
{
    internal class TeslaFleetServices
    {
        public static async Task<string> HttpGetRequestAsync(string base_url, string endpoint, string access_token, CancellationTokenSource cts)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access_token);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await client.GetAsync(base_url + endpoint);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        return responseContent;
                    }
                    else
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        return response.StatusCode.ToString();
                    }
                }
                catch (Exception ex)
                {
                    return null;
                    //return $"Exception: {ex.Message}";
                }
            }
        }

        public static async Task<string> HttpPostRequestAsync(string base_url, string endpoint, string access_token, CancellationTokenSource cts)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access_token);
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        //var postData = new FormUrlEncodedContent(new[]
                        //{
                        //    new KeyValuePair<string, string>("Content-Type", "application/json")
                        //});

                        var response = await client.PostAsync(base_url + endpoint, null);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            return responseContent;
                        }
                        else
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            return response.StatusCode.ToString();
                            //return $"Error: {response.StatusCode}";
                        }
                    }
                    catch (Exception ex)
                    {
                        return null;
                        //return $"Exception: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static async Task<string> GenerateAuthorizeUriAsync(string requestUrl, string client_id)
        {
            //Uri requestUri = new Uri("https://auth.tesla.cn/oauth2/v3/token");
            try
            {
                string state = Guid.NewGuid().ToString();
                string teslaAuthURL = requestUrl + "?client_id=" + client_id + "&prompt=login"
                    + "&redirect_uri=" + "http%3A%2F%2Flocalhost%3A8000%2Fcallback"
                    + "&response_type=code"
                    + "&scope="
                    + "openid"
                    + "%20offline_access"
                    + "%20user_data"
                    + "%20vehicle_device_data"
                    + "%20vehicle_cmds"
                    + "%20vehicle_charging_cmds"
                    + "&state=" + state;

                //System.Uri StartUri = new Uri(teslaAuthURL);
                //System.Uri EndUri = new Uri("teslauwp://localhost:8000/callback");
                return teslaAuthURL;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> RefreshTokenRequestAsync(Uri requestUri, string client_id, string refresh_token, CancellationTokenSource cts)
        {
            //Uri requestUri = new Uri("https://auth.tesla.cn/oauth2/v3/token");
            try
            {
                var postData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", client_id),
                    new KeyValuePair<string, string>("refresh_token", refresh_token)
                });

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await client.PostAsync(requestUri, postData);
                    var content = await response.Content.ReadAsStringAsync();
                    return content;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
