using Azure;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.Threading;
using System.Net.Http;
using System.IO;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;

public static class PhiDetectionFunction
{

    [FunctionName("PhiDetection")]
    public static async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        try
        {
            // Load the .env file
            Env.Load();

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            ILogger Logger = loggerFactory.CreateLogger("PhiDetectionFunction");

            // Load Endpoints from config file
            string cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOSDB_ENDPOINT");

            // Load Cosmos DB name from config file
            string cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOSDB_DBNAME");

            // Load Credentials
            var credentials = new DefaultAzureCredential();

            // Cosmos DB Client Options
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Session,
                ApplicationName = "YourAppName"
            };

            // Create a search index client.
            var cosmosClient = new CosmosClient(cosmosEndpoint, credentials, cosmosClientOptions);

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

            // // Optional: Log the indexing policy for debugging
            // Console.WriteLine("Indexing Policy:");
            // Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(indexingPolicy));

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

        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    [FunctionName("HealthCheck")]
    public static IActionResult HealthCheck([HttpTrigger(AuthorizationLevel.Function, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult("Healthy");
    }

    /// <summary>
    /// Model class that represents a PHI record.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public record PHIRecord
    {   public string id { get; set; }
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
