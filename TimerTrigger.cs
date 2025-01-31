using System;
using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
//using Azure.Storage.Blobs;

namespace MSFT.Function
{
    public class TimerTrigger
    {
        private readonly ILogger _logger;
        private DateTime tokenExpiryTime;
        private DefaultAzureCredential credentials;
        //private BlobServiceClient blobClient;

        private CosmosClient cosmosClient;

        public TimerTrigger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TimerTrigger>();
            string cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOSDB_ENDPOINT")!;
            //string blobEndpoint = Environment.GetEnvironmentVariable("BLOB_STORAGE_ENDPOINT")!;
            try
            {
                var options = new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = Environment.GetEnvironmentVariable("MANAGED_IDENTITY_CLIENT_ID")
                };
                credentials = new DefaultAzureCredential(options);
                cosmosClient = new CosmosClient(cosmosEndpoint, credentials, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationName = "YourAppName"
                });
                //blobClient = new BlobServiceClient(new Uri(blobEndpoint), credentials);
                tokenExpiryTime = DateTime.UtcNow.AddMinutes(55);
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                _logger.LogError(ex, "ManagedIdentityCredential authentication failed. Check Managed Identity configuration.");
                throw;
            }
        }

        [Function("TimerTrigger")]
        public async Task Run([TimerTrigger("* * * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            // Load Endpoints from config file
            string cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOSDB_ENDPOINT")!;

            // Load Cosmos DB name from config file
            string cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOSDB_DBNAME")!;

            // Refresh credentials and CosmosClient if token is about to expire
            if (DateTime.UtcNow >= tokenExpiryTime)
            {
                try
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
                catch (Azure.Identity.AuthenticationFailedException ex)
                {
                    _logger.LogError(ex, "ManagedIdentityCredential authentication failed.");
                    throw;
                }
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
            var containerProperties = new ContainerProperties("phirecords-v1", "/id")
            {
                IndexingPolicy = indexingPolicy
            };

            // Ensure the Cosmos DB database exists and create container with custom indexing policy
            var containerResponse = await cosmosDatabase.Database.CreateContainerIfNotExistsAsync(containerProperties).ConfigureAwait(false);
            var container = containerResponse.Container;
            _logger.LogInformation($"*** Container: {container}");

            // Retrieve LANGUAGE_ENDPOINT and LANGUAGE_KEY from environment variables.
            string languageEndpoint = Environment.GetEnvironmentVariable("LANGUAGE_ENDPOINT")!;
            string languageKey = Environment.GetEnvironmentVariable("LANGUAGE_KEY")!;

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
                    _logger.LogError($"Failed to call language endpoint: {languageResponse.StatusCode}");
                } else {
                    // Get the response content
                    var languageResponseContent = await languageResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Language response: {languageResponseContent}"); 
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
                            string category = entity.GetProperty("category").GetString() ?? string.Empty;
                            // Create a PHIRecord with category
                            var record = new PHIRecord("LanguageSubscription", "LanguageRG", "LanguageStorage", "Container", "FromLanguage", "insert", category, category);
                            entities.Add(record);
                        }
                    }
                    await Task.WhenAll(entities.Select(e => container.UpsertItemAsync(e)));
                }
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
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
