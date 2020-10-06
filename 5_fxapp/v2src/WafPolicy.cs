using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sample
{
    public static class WafPolicy
    {
        [FunctionName("BlockIPWafHttp")]
        public static async Task<HttpResponseMessage> BlockIPWafHttp(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
          string requestBody = await req.Content.ReadAsStringAsync();
            Payload data = JsonConvert.DeserializeObject<Payload>(requestBody);
            /* request body format
            {
                "blockip": "10.10.10.10"
            }
            */

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("BlockIPWafOrchestration", data);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("BlockIPWafOrchestration")]
        public static async Task<string> BlockIPWafOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Payload data = context.GetInput<Payload>();

            RequestParam reqparam = new RequestParam {
                action = "add", 
                blockips = new List<string> { $"{data.blockip}" }
            };

            //var output1 = await context.CallActivityAsync<string>("UpdateWafPolicy", reqparam);
            //var output2 = await context.CallActivityAsync<string>("InsertToDB", reqparam);

            //run parallel            
            var tasks = new Task<string>[2];
            tasks[0] = context.CallActivityAsync<string>("UpdateWafPolicy", reqparam);
            tasks[1] = context.CallActivityAsync<string>("InsertToDB", reqparam);

            await Task.WhenAll(tasks);

            return tasks[0].Result;
        }

        [Singleton(Mode=SingletonMode.Function)]
        [FunctionName("UpdateWafPolicy")]
        public static async Task<string> UpdateWafPolicy([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            var param = context.GetInput<RequestParam>();

            string subscriptionid = Environment.GetEnvironmentVariable("subscriptionid");
            string resourcegroup = Environment.GetEnvironmentVariable("resourcegroup");
            string resourcename = Environment.GetEnvironmentVariable("resourcename");
            string clientid = Environment.GetEnvironmentVariable("clientid");

            log.LogInformation($"UpdateWafPolicy function started at {DateTime.Now}: action={param.action}.");

            string access_token = await Utils.GetToken("https://management.azure.com/", clientid);
            var res = await GetWafPolicy(subscriptionid, resourcegroup, resourcename, access_token);
            // Use Dynamic type
            dynamic wafPolicy = JsonConvert.DeserializeObject(res);

            string policy = ""; 
            if (param.action.ToLower() == "add")
            {
                string blockip = param.blockips[0];

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
                                matchValues = new List<string> { $"{blockip}" }
                            }
                        }
                    };

                    wafPolicy.properties.customRules.Add(JObject.FromObject(initRule));
                }
                else
                {
                    // find `IPBlock` rule
                    for (int i = 0; i < wafPolicy.properties.customRules.Count; i++)
                    {
                        // update IPs list (assume only one matchcondition)
                        if (wafPolicy.properties.customRules[i].name == "IPBlock")
                        {
                            wafPolicy.properties.customRules[i].matchConditions[0].matchValues.Add(blockip);
                            break;
                        }
                    }
                }

                string newPolicy = JsonConvert.SerializeObject(wafPolicy);
                policy = await PutWafPolicy(subscriptionid, resourcegroup, resourcename, access_token, newPolicy);
            }
            else // remove
            {
                // find `IPBlock` rule
                for (int i =0; i < wafPolicy.properties.customRules.Count; i++)
                {
                    // delete IPs where ttl <= 0 (assume only one matchcondition)
                    if (wafPolicy.properties.customRules[i].name == "IPBlock")
                    {
                        List<string> newList = wafPolicy.properties.customRules[i].matchConditions[0].matchValues.ToObject<List<string>>();
                        newList.RemoveAll(x => param.blockips.Contains(x));
                        if (newList.Count == 0) // no blockip then remove `IPBlock` custom rule
                            wafPolicy.properties.customRules.RemoveAt(i);
                        else
                            wafPolicy.properties.customRules[i].matchConditions[0].matchValues = JArray.FromObject(newList);

                        string newPolicy = JsonConvert.SerializeObject(wafPolicy);
                        policy = await PutWafPolicy(subscriptionid, resourcegroup, resourcename, access_token, newPolicy);
                        
                        break;
                    }
                }
            }

            return $"{policy}";
        }


        static async Task<string> GetWafPolicy(string subscriptionId, string resourceGroupName, string resourceName, string accessToken)
        {
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
            string accessToken, string newPolicy)
        {

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
    }


    // customize your payload
    class RequestParam
    {
        public string action { get; set;}
        public List<string> blockips { get; set; }
    }

    class CustomRule
    {
        public string name { get; set; }
        public string priority { get; set; }
        public string ruleType { get; set; }
        public string action { get; set; }

        public List<MatchCondition> matchConditions { get; set; }
    }

    class MatchCondition
    {
        public List<MatchVariable> matchVariables { get; set; }
        [JsonProperty("operator")]
        public string _operator { get; set; }
        public bool negationConditon { get; set; }
        public List<string> matchValues { get; set; }
    }

    class MatchVariable
    {
        public string variableName { get; set; }
    }

}