#r "Newtonsoft.Json"

using System.Net;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

using System.Text;
using System.Net.Http;

using System.Data.SqlClient;

// https://www.c-sharpcorner.com/article/working-with-timer-trigger-azure-functions/
public static async Task Run(TimerInfo myTimer, ILogger log)
{
    log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

    string sqlservername = "_add_here_";
    string databasename = "_add_here_";
    string clientid = "_add_here_";

    var response = await GetToken("https://database.windows.net/", clientid);
    var res = await response.Content.ReadAsStringAsync();
    var token = JsonConvert.DeserializeObject<Token>(res);

    string value = await InsertSQL(sqlservername, databasename, token.access_token);

    log.LogInformation($"Completed at {DateTime.Now}: {value}");
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

static async Task<string> InsertSQL(string servername, string database, string accessToken)  {

    string value = "";

    if (accessToken != null) {
        string connectionString = $"Data Source={servername}; Initial Catalog={database};";
 
        using(SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.AccessToken = accessToken;
            conn.Open();
            int id = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            string fn = $"firstname_{id}";
            string ln = $"lastname_{id}";

            SqlCommand command = new SqlCommand($"INSERT INTO test_table VALUES ('{id}', '{fn}', '{ln}')", conn);
            var rows = await command.ExecuteNonQueryAsync();
            value = $"{rows} rows were updated";

        }
         
    }

    return value;
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
