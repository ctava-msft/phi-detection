using System;
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
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
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


            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
