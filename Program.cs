using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using DotNetEnv;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings;
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

// Program class    
[RequiresPreviewFeatures]
class Program
{

    // Main method
    static async Task Main(string[] args)
    {
        try
        {

            // Create a LoggerFactory and Logger
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();

            // Load the .env file
            Env.Load();

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

            // Create phirecords entries
            var phiRecords = CreatePHIRecords().ToList();

            // Upsert the entries into the collection.
            var tasks = phiRecords.Select(async x =>
            {
                // Log the PHIRecord as JSON
                // Console.WriteLine($"Upserting PHIRecord: {System.Text.Json.JsonSerializer.Serialize(x)}");
                
                // Validate required fields
                if (string.IsNullOrEmpty(x.id) || string.IsNullOrEmpty(x.FileName) || string.IsNullOrEmpty(x.Operation))
                {
                    Console.WriteLine($"Invalid PHIRecord detected: {System.Text.Json.JsonSerializer.Serialize(x)}");
                    throw new ArgumentException("PHIRecord contains invalid or missing fields.");
                }

                // Validate Partition Key
                if (string.IsNullOrEmpty(x.id))
                {
                    Console.WriteLine($"Partition key 'id' is missing for PHIRecord: {System.Text.Json.JsonSerializer.Serialize(x)}");
                    throw new ArgumentException("Partition key 'id' is required.");
                }

                int maxRetries = 3;
                int attempt = 0;
                while (attempt < maxRetries)
                {
                    try
                    {
                        Console.WriteLine($"id: '{x.id}' file:'{x.FileName}' operation:'{x.Operation}' fieldname:'{x.FieldName}' fieldtype:'{x.FieldType}'");
                        await container.UpsertItemAsync<PHIRecord>(item: x);
                        return x.id;
                    }
                    catch (CosmosException cosmosEx) when (cosmosEx.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        Console.WriteLine($"BadRequest error upserting entry '{x.id}': {cosmosEx.Message}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        attempt++;
                        Console.WriteLine($"Error upserting entry '{x.id}': {ex.Message} (Attempt {attempt}/{maxRetries})");
                        if (attempt >= maxRetries)
                        {
                            Console.WriteLine($"Failed to upsert entry '{x.id}' after {maxRetries} attempts.");
                            throw; //return null; // or handle accordingly
                        }
                        await Task.Delay(1000); // Wait before retrying
                    }
                }
                return null;
            });
            await Task.WhenAll(tasks);

            // // Retrieve LANGUAGE_ENDPOINT and LANGUAGE_KEY from environment variables.
            // string languageEndpoint = Environment.GetEnvironmentVariable("LANGUAGE_ENDPOINT");
            // string languageKey = Environment.GetEnvironmentVariable("LANGUAGE_KEY");
            // using (var httpClient = new HttpClient())
            // {
            //     httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", languageKey);
            //     string payloadJson = File.ReadAllText("lang.json");
            //     var content = new StringContent(
            //         payloadJson,
            //         System.Text.Encoding.UTF8,
            //         "application/json"
            //     );
            //     var languageResponse = await httpClient.PostAsync(
            //         $"{languageEndpoint}/language/:analyze-text?api-version=2022-05-01",
            //         content
            //     );
            //     if (!languageResponse.IsSuccessStatusCode)
            //     {
            //         logger.LogError($"Failed to call language endpoint: {languageResponse.StatusCode}");
            //     } else {
            //         var languageResponseContent = await languageResponse.Content.ReadAsStringAsync();
            //         logger.LogInformation($"Language response: {languageResponseContent}"); 
            //     }
            // }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
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

    /// <summary>
    /// Create some sample PHI records.
    /// </summary>
    /// <returns>A list of sample PHI records.</returns>
    private static IEnumerable<PHIRecord> CreatePHIRecords()
    {
        yield return new PHIRecord("YourSubscription", "YourResourceGroup", "YourStorageAreaName", "YourStorageAreaContainer", "YourFileName", "insert", "Name", "A");
    
        yield return new PHIRecord("YourSubscription", "YourResourceGroup", "YourStorageAreaName", "YourStorageAreaContainer", "YourFileName", "update", "Email", "F");
    
        yield return new PHIRecord("YourSubscription", "YourResourceGroup", "YourStorageAreaName", "YourStorageAreaContainer", "YourFileName", "delete", "IP Address", "O");
    }
}
