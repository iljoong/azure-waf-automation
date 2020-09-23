#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

using System.Text;
using System.Net.Http;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("BlockIPWaf function processed a request.");

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    Payload data = JsonConvert.DeserializeObject<Payload>(requestBody);
/* payload format
{
    "subscriptionid": "{subscription id}",
    "resourcegroup": "{resourcegroup name}",
    "resourcename": "{policy name}",
    "clientid": "{client id of identity}",
    "blockips": [
        "10.10.10.10",
        "11.11.11.11"
    ]
}
*/
    string subscriptionid = data.subscriptionid;
    string resourcegroup = data.resourcegroup;
    string resourcename = data.resourcename;
    string blockips = JsonConvert.SerializeObject(data.blockips);

    var response = await GetToken("https://management.azure.com/", data.clientid);
    var res = await response.Content.ReadAsStringAsync();
    var token = JsonConvert.DeserializeObject<Token>(res);

    //string policy = await GetWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token);
    string policy = await PutWafPolicy(subscriptionid, resourcegroup, resourcename, token.access_token, blockips);

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
    string accessToken, string blockips)  {

    string _url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/{resourceName}?api-version=2020-06-01";
    var _requestContent = @"{
  ""location"": ""koreacentral"", 
  ""properties"": {
    ""customRules"": [
      {
        ""name"": ""IPBlock"",
        ""priority"": 100,
        ""ruleType"": ""MatchRule"",
        ""action"": ""Block"",
        ""matchConditions"": [
          {
            ""matchVariables"": [
              {
                ""variableName"": ""RemoteAddr""
              }
            ],
            ""operator"": ""IPMatch"",
            ""negationConditon"": false,
            ""matchValues"": {blockips},
            ""transforms"": []
          }
        ],
        ""skippedManagedRuleSets"": []
      }
    ],
    ""policySettings"": {
      ""requestBodyCheck"": true,
      ""maxRequestBodySizeInKb"": 128,
      ""fileUploadLimitInMb"": 100,
      ""state"": ""Enabled"",
      ""mode"": ""Prevention""
    },
    ""managedRules"": {
      ""managedRuleSets"": [
        {
          ""ruleSetType"": ""OWASP"",
          ""ruleSetVersion"": ""3.0"",
          ""ruleGroupOverrides"": []
        }
      ],
      ""exclusions"": []
    }
  }
}";

    using (var _client = new HttpClient())
    {
        var request = new HttpRequestMessage(HttpMethod.Put, _url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        string requestContent = _requestContent.Replace("{blockips}", blockips);
        request.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        
        return res;
    }
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

class Payload
{
    public string subscriptionid { get; set; }
    public string resourcegroup { get; set; }
    public string resourcename { get; set; }
    public string clientid { get; set; }
    public List<string> blockips { get; set; }
}

