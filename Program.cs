using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
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
                .ConfigureServices(services =>
                {
                    services.AddHealthChecks();
                })
                .Build();

            host.Run();
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/health")]
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

        public TimerTriggerFunction(ILogger<TimerTriggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("TimerTriggerFunction")]
        public Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");
            // Add your logic here
            return Task.CompletedTask;
        }
    }
}