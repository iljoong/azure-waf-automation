#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

using System;
using System.Text;
using System.Net.Http;

using System.Data;
using System.Data.SqlClient;

// https://www.c-sharpcorner.com/article/working-with-timer-trigger-azure-functions/
public static async Task Run(TimerInfo myTimer, ILogger log)
{
    log.LogInformation($"TTLTimer started at: {DateTime.Now}");

    string subscriptionid = Environment.GetEnvironmentVariable("subscriptionid");
    string resourcegroup = Environment.GetEnvironmentVariable("resourcegroup");
    string resourcename = Environment.GetEnvironmentVariable("resourcename");
    string clientid = Environment.GetEnvironmentVariable("clientid");
    string sqlserverfqdn = Environment.GetEnvironmentVariable("sqlserverfqdn");
    string databasename = Environment.GetEnvironmentVariable("databasename");

    var response = await GetToken("https://database.windows.net/", clientid);
    var res = await response.Content.ReadAsStringAsync();
    var token = JsonConvert.DeserializeObject<Token>(res);

    List<string> ttl0IPs = await GetTTL0IPsfromDB(sqlserverfqdn, databasename, token.access_token);
    if (ttl0IPs.Count > 0)
    {
        // get token of waf
        response = await GetToken("https://management.azure.com/", clientid);
        res = await response.Content.ReadAsStringAsync();
        token = JsonConvert.DeserializeObject<Token>(res);

        //get waf policy
        res = await GetWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token);
        // Use Dynamic type
        dynamic wafPolicy = JsonConvert.DeserializeObject(res);

        // find `IPBlock` rule
        for (int i =0; i < wafPolicy.properties.customRules.Count; i++)
        {
            // delete IPs where ttl <= 0
            if (wafPolicy.properties.customRules[i].name == "IPBlock")
            {
                List<string> newList = wafPolicy.properties.customRules[i].matchConditions[0].matchValues.ToObject<List<string>>();
                newList.RemoveAll(x => ttl0IPs.Contains(x));
                if (newList.Count == 0) // no blockip then remove `IPBlock` custom rule
                    wafPolicy.properties.customRules.RemoveAt(i);
                else
                    wafPolicy.properties.customRules[i].matchConditions[0].matchValues = JArray.FromObject(newList);

                break;
            }
        }

        string newPolicy = JsonConvert.SerializeObject(wafPolicy);
        string policy = await PutWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token, newPolicy);

        log.LogInformation($"Completed at {DateTime.Now}: removed {ttl0IPs.Count} blockip(s)");
    }
    else
    {
        log.LogInformation($"Completed at {DateTime.Now}: nothing removed");    
    }
}

static async Task<HttpResponseMessage> GetToken(string resource, string clientid)  {

    // For UserAssigned identity, you must provide `client_id` parameter!!!
    //https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?toc=%2Fazure%2Fazure-functions%2Ftoc.json&tabs=dotnet#using-the-rest-protocol
    using (var _client = new HttpClient())
    {
        var request = new HttpRequestMessage(HttpMethod.Get, 
            String.Format("{0}/?resource={1}&client_id={2}&api-version=2019-08-01", 
            Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"),
            resource, clientid));
        request.Headers.Add("X-IDENTITY-HEADER", Environment.GetEnvironmentVariable("IDENTITY_HEADER"));
        return await _client.SendAsync(request);
    }
}

static async Task<string> GetWafPolicy(string subscriptionId, string resourceGroupName, string resourceName, string accessToken)  {
    string _url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/{resourceName}?api-version=2020-06-01";
    
    using (var _client = new HttpClient())
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        var response = await _client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        
        return res;
    }
}

static async Task<string> PutWafPolicy(string subscriptionId, string resourceGroupName, string resourceName,
    string accessToken, string newPolicy)  {

    string _url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/{resourceName}?api-version=2020-06-01";

    using (var _client = new HttpClient())
    {
        var request = new HttpRequestMessage(HttpMethod.Put, _url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = new StringContent(newPolicy, Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        
        return res;
    }
}

static async Task<List<string>> GetTTL0IPsfromDB(string servername, string database, string accessToken)  {

    List<String> TTL0IPs = new List<String>();

    if (accessToken != null) {
        string connectionString = $"Data Source={servername}; Initial Catalog={database};";
 
        using(SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.AccessToken = accessToken;
            conn.Open();
            SqlCommand command = new SqlCommand("UPDATE WAFBLOCKIP SET TTL = TTL - 1", conn);
            var rows = await command.ExecuteNonQueryAsync();

            command = new SqlCommand("select IP_ADDR from WAFBLOCKIP where TTL <= 0", conn);
            SqlDataReader reader = command.ExecuteReader();
            while(reader.Read())
            {
                TTL0IPs.Add(reader.GetString(0));
            }
            reader.Close();

            command = new SqlCommand("delete from WAFBLOCKIP where TTL <=-5", conn);
            rows = await command.ExecuteNonQueryAsync();
        }
    }

    return TTL0IPs;
}

class Token
{
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public int ext_expires_in { get; set; }
    public int expires_on { get; set; }
    public int not_before { get; set; }
    public string resource { get; set; }
    public string access_token { get; set; }
}
