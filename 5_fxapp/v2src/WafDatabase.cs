using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Data.SqlClient;

namespace Sample
{
    public static class WafDB
    {
        [FunctionName("UpdateTTLTimerHttp")]
        public static async Task<HttpResponseMessage> UpdateTTLTimerHttp(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("TTLTimerOrchestration", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("TTLTimerTrigger")]
        public static async Task TTLTimerTrigger([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            string instanceId = await starter.StartNewAsync("TTLTimerOrchestration", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        [FunctionName("TTLTimerOrchestration")]
        public static async Task<string> TTLTimerOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<string> ttllist = await context.CallActivityAsync<List<string>>("UpdateTTL", null);

            string policy = "nothing updated";
            if (ttllist.Count > 0)
            {
                RequestParam reqparam = new RequestParam {
                    action = "remove", 
                    blockips = ttllist
                };
                
                policy = await context.CallActivityAsync<string>("UpdateWafPolicy", reqparam);
            }
            return policy;
        }

        [FunctionName("InsertToDB")]
        public static async Task<int> InsertToDB([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            var param = context.GetInput<RequestParam>();

            string clientid = Environment.GetEnvironmentVariable("clientid");
            string sqlserverfqdn = Environment.GetEnvironmentVariable("sqlserverfqdn");
            string databasename = Environment.GetEnvironmentVariable("databasename");

            var access_token = await Utils.GetToken("https://database.windows.net/", clientid);
            int rows = await InsertIPtoDB(sqlserverfqdn, databasename, access_token, param.blockips[0]);

            log.LogInformation($"InsertToDB function completed at {DateTime.Now}: {param.blockips[0]} added, {rows} updated");

            return rows;
        }

        [FunctionName("UpdateTTL")]
        public static async Task<List<string>> UpdateTTL([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            string clientid = Environment.GetEnvironmentVariable("clientid");
            string sqlserverfqdn = Environment.GetEnvironmentVariable("sqlserverfqdn");
            string databasename = Environment.GetEnvironmentVariable("databasename");

            var access_token = await Utils.GetToken("https://database.windows.net/", clientid);

            List<string> ttl0IPs = await GetTTL0IPsfromDB(sqlserverfqdn, databasename, access_token);

            log.LogInformation($"UpdateTTL function completed at {DateTime.Now}");

            return ttl0IPs;
        }

        static async Task<List<string>> GetTTL0IPsfromDB(string servername, string database, string accessToken)
        {

            List<String> TTL0IPs = new List<String>();

#if LOCALDEV
            string connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION");
#else
            string connectionString = $"Data Source={servername}; Initial Catalog={database};";
#endif
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
#if !LOCALDEV               
                conn.AccessToken = accessToken;
#endif
                conn.Open();
                SqlCommand command = new SqlCommand("UPDATE WAFBLOCKIP SET TTL = TTL - 1", conn);
                var rows = await command.ExecuteNonQueryAsync();

                command = new SqlCommand("select IP_ADDR from WAFBLOCKIP where TTL <= 0", conn);
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    TTL0IPs.Add(reader.GetString(0));
                }
                reader.Close();

                command = new SqlCommand("delete from WAFBLOCKIP where TTL <=-5", conn);
                rows = await command.ExecuteNonQueryAsync();
            }

            return TTL0IPs;
        }

        static async Task<int> InsertIPtoDB(string servername, string database, string accessToken, string blockIP)
        {
            int rows = 0;

#if LOCALDEV
            string connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION");
#else
            string connectionString = $"Data Source={servername}; Initial Catalog={database};";
#endif
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
#if !LOCALDEV               
                conn.AccessToken = accessToken;
#endif
                conn.Open();

                SqlCommand command = new SqlCommand(@$"
                    IF EXISTS(select IP_ADDR from WAFBLOCKIP where IP_ADDR = '{blockIP}')
                    BEGIN
                        update WAFBLOCKIP SET TTL = 15 where IP_ADDR = '{blockIP}'
                    END
                    ELSE
                    BEGIN
                        insert into WAFBLOCKIP (IP_ADDR, TTL) values('{blockIP}', 15)
                    END", conn);
                rows = await command.ExecuteNonQueryAsync();
            }

            return rows;
        }
    }
}