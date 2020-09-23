#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

using System.Data.SqlClient;

using System.Text;
using System.Net.Http;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("TestSQL function processed a request.");

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    Payload data = JsonConvert.DeserializeObject<Payload>(requestBody);
/* payload format
{
    "sqlservername": "{subscription id}",
    "databasename": "{resourcegroup name}",
    "clientid": "{client id of indentity}"
}
*/

    log.LogInformation($"{data.sqlservername}, {data.databasename}");

    var response = await GetToken("https://database.windows.net/", data.clientid);
    var res = await response.Content.ReadAsStringAsync();
    var token = JsonConvert.DeserializeObject<Token>(res);

    string value = await GetSQL(data.sqlservername, data.databasename, token.access_token);
    //string value = await InsertSQL(data.sqlservername, data.databasename, token.access_token);

    return new OkObjectResult(value);
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

static async Task<string> GetSQL(string servername, string database, string accessToken)  {

    string value = "";

    if (accessToken != null) {
        string connectionString = $"Data Source={servername}; Initial Catalog={database};";
 
        using(SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.AccessToken = accessToken;
            conn.Open();

            using (SqlCommand command = new SqlCommand("SELECT top 10 * FROM test_table ORDER BY id DESC;", conn))
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                while (reader.Read())
                {
                    value += string.Format("{0} {1} {2}\r\n",
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2));
                }
            }
        }
         
    }

    return value;
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
            string fn = "first name";
            string ln = "last name";

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

class Payload
{
    public string sqlservername { get; set; }
    public string databasename { get; set; }
    public string clientid { get; set; }
}
