using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyFunctionApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                 // For the isolated worker model
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureServices(services =>
                {
                    services.AddHealthChecks();
                })
                .Build();

            try
            {
                host.Run();
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "The listener for function 'Functions.TimerTriggerFunction' was unable to start.");
                throw;
            }
        }
    }

    public class HealthCheckFunction
    {
        private readonly ILogger<HealthCheckFunction> _logger;

        public HealthCheckFunction(ILogger<HealthCheckFunction> logger)
        {
            _logger = logger;
        }

        [Function("HealthCheck")]
        public HttpResponseData Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
            HttpRequestData req)
        {
            _logger.LogInformation("Health check endpoint hit.");
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.WriteString("Healthy");
            return response;
        }
    }

    public class TimerTriggerFunction
    {
        private readonly ILogger<TimerTriggerFunction> _logger;
        private DefaultAzureCredential credentials = new DefaultAzureCredential();
        private CosmosClient cosmosClient;
        private DateTime tokenExpiryTime = DateTime.MinValue;

        public TimerTriggerFunction(ILogger<TimerTriggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("TimerTriggerFunction")]
        public Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            try
            {
                // Load the .env file
                Env.Load();

                _logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");

                // Load Endpoints from config file
                string cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOSDB_ENDPOINT");

                // Load Cosmos DB name from config file
                string cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOSDB_DBNAME");

                // Refresh credentials and CosmosClient if token is about to expire
                if (DateTime.UtcNow >= tokenExpiryTime)
                {
                    credentials = new DefaultAzureCredential();
                    cosmosClient = new CosmosClient(cosmosEndpoint, credentials, new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        ConsistencyLevel = ConsistencyLevel.Session,
                        ApplicationName = "YourAppName"
                    });
                    tokenExpiryTime = DateTime.UtcNow.AddMinutes(55); // Set token expiry time to 55 minutes from now
                }

                // Ensure the Cosmos DB database exists
                var cosmosDatabase = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabaseName).ConfigureAwait(false);

                // Define the custom indexing policy
                var indexingPolicy = new Microsoft.Azure.Cosmos.IndexingPolicy
                {
                    IndexingMode = Microsoft.Azure.Cosmos.IndexingMode.Consistent,
                    Automatic = true
                };

                // Add included paths
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/Subscription/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/ResourceGroup/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/StorageAreaName/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/StorageAreaContainer/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/FileName/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/Operation/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/FieldName/?" });
                indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/FieldType/?" });
                indexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/_etag/?" });

                // Create container properties with the custom indexing policy
                var containerProperties = new ContainerProperties("phirecords-v9", "/id")
                {
                    IndexingPolicy = indexingPolicy
                };

                // Ensure the Cosmos DB database exists and create container with custom indexing policy
                var containerResponse = await cosmosDatabase.Database.CreateContainerIfNotExistsAsync(containerProperties).ConfigureAwait(false);
                var container = containerResponse.Container;

                // Retrieve LANGUAGE_ENDPOINT and LANGUAGE_KEY from environment variables.
                string languageEndpoint = Environment.GetEnvironmentVariable("LANGUAGE_ENDPOINT");
                string languageKey = Environment.GetEnvironmentVariable("LANGUAGE_KEY");

                // Call the language endpoint
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", languageKey);
                    string payloadJson = File.ReadAllText("lang.json");
                    var content = new StringContent(
                        payloadJson,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                    var languageResponse = await httpClient.PostAsync(
                        $"{languageEndpoint}/language/:analyze-text?api-version=2022-05-01",
                        content
                    );
                    if (!languageResponse.IsSuccessStatusCode)
                    {
                        Logger.LogError($"Failed to call language endpoint: {languageResponse.StatusCode}");
                    } else {
                        // Get the response content
                        var languageResponseContent = await languageResponse.Content.ReadAsStringAsync();
                        Logger.LogInformation($"Language response: {languageResponseContent}"); 
                        var doc = System.Text.Json.JsonDocument.Parse(languageResponseContent);
                        var entities = new List<PHIRecord>();
                        foreach (var docElement in doc.RootElement
                            .GetProperty("results")
                            .GetProperty("documents")
                            .EnumerateArray())
                        {
                            foreach (var entity in docElement.GetProperty("entities").EnumerateArray())
                            {
                                // string text = entity.GetProperty("text").GetString();
                                string category = entity.GetProperty("category").GetString();
                                // Create a PHIRecord with category
                                var record = new PHIRecord("LanguageSubscription", "LanguageRG", "LanguageStorage", "Container", "FromLanguage", "insert", category, category);
                                entities.Add(record);
                            }
                        }
                        await Task.WhenAll(entities.Select(e => container.UpsertItemAsync(e)));
                    }
                }

                return Task.CompletedTask;


            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }



        }
    }

    /// <summary>
    /// Model class that represents a PHI record.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public record PHIRecord
    {   
        public string id { get; set; }
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; }
        public string StorageAreaName { get; set; }
        public string StorageAreaContainer { get; set; }
        public string FileName { get; set; }
        public string Operation { get; set; } 
        public string FieldName { get; set; } 
        public string FieldType { get; set; }

        public PHIRecord() { }

        public PHIRecord(string subscription, string resourceGroup, string storageAreaName, string storageAreaContainer, string fileName, string operation, string fieldName, string fieldType)
        {
            id = Guid.NewGuid().ToString();
            Subscription = subscription;
            ResourceGroup = resourceGroup;
            StorageAreaName = storageAreaName;
            StorageAreaContainer = storageAreaContainer;
            FileName = fileName;
            Operation = operation;
            FieldName = fieldName;
            FieldType = fieldType;
        }

    }    
}