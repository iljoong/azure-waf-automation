using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json;

namespace Sample
{
    public class Utils
    {
        static public async Task<string> GetToken(string resource, string clientid)
        {
#if LOCALDEV
            if (resource == "https://management.azure.com/")
                return await GetTokenLocal();
            else
                return "NOTOKEN";
#else
            // For UserAssigned identity, you must provide `client_id` parameter!!!
            //https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?toc=%2Fazure%2Fazure-functions%2Ftoc.json&tabs=dotnet#using-the-rest-protocol
            using (var _client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    String.Format("{0}/?resource={1}&client_id={2}&api-version=2019-08-01",
                    Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"),
                    resource, clientid));
                request.Headers.Add("X-IDENTITY-HEADER", Environment.GetEnvironmentVariable("IDENTITY_HEADER"));
                
                var response = await _client.SendAsync(request);
                var res = await response.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<Token>(res);

                return token.access_token;
            }
#endif
        }

        static public async Task<string> GetTokenLocal()
        {
            string tenantid = Environment.GetEnvironmentVariable("AZ_TENANTID");
            string clientid = Environment.GetEnvironmentVariable("AZ_CLIENTID");
            string secret = Environment.GetEnvironmentVariable("AZ_SECRET");

            using (var _client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://login.microsoftonline.com/{tenantid}/oauth2/token")
                {
                    Content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("grant_type", "client_credentials"),
                            new KeyValuePair<string, string>("resource", "https://management.azure.com/"),
                            new KeyValuePair<string, string>("client_id", clientid),
                            new KeyValuePair<string, string>("client_secret", secret)
                        })
                };
                request.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                var response = await _client.SendAsync(request);
                var res = await response.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<Token>(res);

                return token.access_token;
            }
        }
    }

    public class Payload
    {
        public string blockip { get; set; }
    }

    public class Token
    {
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public int ext_expires_in { get; set; }
        public int expires_on { get; set; }
        public int not_before { get; set; }
        public string resource { get; set; }
        public string access_token { get; set; }
    }
}