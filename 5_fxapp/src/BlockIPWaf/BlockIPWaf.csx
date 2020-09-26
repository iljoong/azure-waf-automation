#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text;
using System.Net.Http;

using System.Data.SqlClient;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    Payload data = JsonConvert.DeserializeObject<Payload>(requestBody);
/* request body format
{
    "blockip": "10.10.10.10"
}
*/
    string subscriptionid = Environment.GetEnvironmentVariable("subscriptionid");
    string resourcegroup = Environment.GetEnvironmentVariable("resourcegroup");
    string resourcename = Environment.GetEnvironmentVariable("resourcename");
    string clientid = Environment.GetEnvironmentVariable("clientid");
    string sqlserverfqdn = Environment.GetEnvironmentVariable("sqlserverfqdn");
    string databasename = Environment.GetEnvironmentVariable("databasename");

    log.LogInformation($"Debug {subscriptionid}, {resourcegroup} {resourcename}, {clientid}, {sqlserverfqdn}, {databasename}");

    log.LogInformation($"BlockIPWaf function started at {DateTime.Now}: {resourcename}, blockip {data.blockip}.");

    var response = await GetToken("https://management.azure.com/", clientid);
    var res = await response.Content.ReadAsStringAsync();
    var token = JsonConvert.DeserializeObject<Token>(res);

    res = await GetWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token);
    // Use Dynamic type
    dynamic wafPolicy = JsonConvert.DeserializeObject(res);

    if (wafPolicy.properties.customRules.Count == 0)
    {
        // handle no 'IPBlock' custom rule exist
        CustomRule initRule = new CustomRule
        {
            name = "IPBlock",
            priority = "50",
            ruleType = "MatchRule",
            action = "Block",
            matchConditions = new List<MatchCondition> {
                new MatchCondition {
                    matchVariables = new List<MatchVariable> {
                        new MatchVariable { variableName = "RemoteAddr" }
                    },
                    _operator = "IPMatch",
                    negationConditon = false,
                    matchValues = new List<string> { $"{data.blockip}" }
                }
            }
        };

        wafPolicy.properties.customRules.Add(JObject.FromObject(initRule));
    }
    else
    {
        // find `IPBlock` rule
        for (int i =0; i < wafPolicy.properties.customRules.Count; i++)
        {
            // update IPs list (assume only one matchcondition)
            if (wafPolicy.properties.customRules[i].name == "IPBlock")
            {
                    wafPolicy.properties.customRules[i].matchConditions[0].matchValues.Add(data.blockip);
            }
        }        
    }

    string newPolicy = JsonConvert.SerializeObject(wafPolicy);
    string policy = await PutWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token, newPolicy);

    response = await GetToken("https://database.windows.net/", clientid);
    res = await response.Content.ReadAsStringAsync();
    token = JsonConvert.DeserializeObject<Token>(res);
    int rows = await InsertIPtoDB(sqlserverfqdn, databasename, token.access_token, data.blockip);
    
    log.LogInformation($"BlockIPWaf function completed at {DateTime.Now}: {data.blockip} added, {rows} updated");

    return new OkObjectResult(policy);
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

static async Task<int> InsertIPtoDB(string servername, string database, string accessToken, string blockIP)  {

    int rows = 0;

    if (accessToken != null) {
        string connectionString = $"Data Source={servername}; Initial Catalog={database};";
 
        using(SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.AccessToken = accessToken;
            conn.Open();
            SqlCommand command = new SqlCommand(@$"
                IF EXISTS(select IP_ADDR from WAFBLOCKIP where IP_ADDR = '{blockIP}')
                BEGIN
                    update WAFBLOCKIP SET TTL = 15 where IP_ADDR = '{blockIP}'
                END
                ELSE
                BEGIN
                    insert into WAFBLOCKIP (IP_ADDR, TTL) values('{blockIP}', 15)
                END
            ", conn);
            rows = await command.ExecuteNonQueryAsync();
        }
    }

    return rows;
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

// customize your payload
class Payload
{
    public string blockip { get; set; }
}

class CustomRule
{
    public string name {get; set;}
    public string priority {get; set;}
    public string ruleType {get; set;}
    public string action {get; set;}
    
    public List<MatchCondition> matchConditions {get; set;}
}

class MatchCondition
{
    public List<MatchVariable> matchVariables {get; set;}
    [JsonProperty("operator")]
    public string _operator {get; set;}
    public bool negationConditon {get; set;}
    public List<string> matchValues {get; set;}
}

class MatchVariable
{
    public string variableName  {get; set;}
}
