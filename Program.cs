using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using DotNetEnv;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.Threading;
using System.Net.Http;
using System.IO;

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

            // // Create a CosmosDBNoSQL vector store.
            // var vectorStore = new AzureCosmosDBNoSQLVectorStore(cosmosDatabase);

            // // Get and create collection if it doesn't exist.
            // var collectionName = "phirecords-v1";
            // var collection = vectorStore.GetCollection<string, PHIRecord>(collectionName);
            // await collection.CreateCollectionIfNotExistsAsync();

            // // Create phirecords entries
            // var phiRecords = CreatePHIRecords().ToList();
            // var tasks = phiRecords.Select(entry => Task.Run(() =>
            // {
            //     Console.WriteLine($"entry: '{entry.Key}' '{entry.Subscription}' '{entry.ResourceGroup}'");
            // }));
            // await Task.WhenAll(tasks);

            // // Upsert the phiRecords into the collection and return their keys.
            // var upsertedKeysTasks = phiRecords.Select(async x =>
            // {
            //     int maxRetries = 5;
            //     int attempt = 0;
            //     while (attempt < maxRetries)
            //     {
            //         try
            //         {
            //             return await collection.UpsertAsync(x);
            //         }
            //         catch (Exception ex)
            //         {
            //             attempt++;
            //             Console.WriteLine($"Error upserting entry '{x.Key}': {ex.Message} (Attempt {attempt}/{maxRetries})");
            //             if (attempt >= maxRetries)
            //             {
            //                 Console.WriteLine($"Failed to upsert entry '{x.Key}' after {maxRetries} attempts.");
            //                 return null; // or handle accordingly
            //             }
            //             // Use exponential backoff with a maximum delay cap
            //             var delay = Math.Min(1000 * (int)Math.Pow(2, attempt), 8000);
            //             await Task.Delay(delay);
            //         }
            //     }
            //     return null;
            // });
            // var upsertedKeys = await Task.WhenAll(upsertedKeysTasks);

            // Retrieve LANGUAGE_ENDPOINT and LANGUAGE_KEY from environment variables.
            string languageEndpoint = Environment.GetEnvironmentVariable("LANGUAGE_ENDPOINT");
            string languageKey = Environment.GetEnvironmentVariable("LANGUAGE_KEY");
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
                    logger.LogError($"Failed to call language endpoint: {languageResponse.StatusCode}");
                } else {
                    var languageResponseContent = await languageResponse.Content.ReadAsStringAsync();
                    logger.LogInformation($"Language response: {languageResponseContent}"); 
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }
   /// <summary>
    /// Sample model class that represents a PHI record.
    /// </summary>
    /// <remarks>
    /// Note that each property is decorated with an attribute that specifies how the property should be treated by the vector store.
    /// This allows us to create a collection in the vector store and upsert and retrieve instances of this class without any further configuration.
    /// </remarks>
    private sealed class PHIRecord
    {
        [VectorStoreRecordKey]
        public string Key { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string Subscription { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string ResourceGroup { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string StorageAreaName { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string StorageAreaContainer { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string Operation { get; set; } // insert, update, or delete

        [VectorStoreRecordData(IsFilterable = true)]
        public string FieldName { get; set; } // name of attribute that has PHI

        [VectorStoreRecordData(IsFilterable = true)]
        public string FieldType { get; set; } // specific Type of PHI classified
    }

    /// <summary>
    /// Create some sample PHI records.
    /// </summary>
    /// <returns>A list of sample PHI records.</returns>
    private static IEnumerable<PHIRecord> CreatePHIRecords()
    {
        yield return new PHIRecord
        {
            Key = "1",
            Subscription = "YourSubscription",
            ResourceGroup = "YourResourceGroup",
            StorageAreaName = "YourStorageAreaName",
            StorageAreaContainer = "YourStorageAreaContainer",
            Operation = "insert",
            FieldName = "Name",
            FieldType = "A"
        };

        yield return new PHIRecord
        {
            Key = "2",
            Subscription = "YourSubscription",
            ResourceGroup = "YourResourceGroup",
            StorageAreaName = "YourStorageAreaName",
            StorageAreaContainer = "YourStorageAreaContainer",
            Operation = "update",
            FieldName = "Email",
            FieldType = "F"
        };

        yield return new PHIRecord
        {
            Key = "3",
            Subscription = "YourSubscription",
            ResourceGroup = "YourResourceGroup",
            StorageAreaName = "YourStorageAreaName",
            StorageAreaContainer = "YourStorageAreaContainer",
            Operation = "delete",
            FieldName = "IP Address",
            FieldType = "O"
        };
    }
}
