using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(MyFunctionApp.Startup))]

namespace MyFunctionApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHealthChecks();
        }
    }

    public class HealthCheckFunction
    {
        private readonly ILogger<HealthCheckFunction> _logger;

        public HealthCheckFunction(ILogger<HealthCheckFunction> logger)
        {
            _logger = logger;
        }

        [FunctionName("HealthCheck")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/health")] HttpRequest req)
        {
            _logger.LogInformation("Health check endpoint hit.");
            return new OkObjectResult("Healthy");
        }
    }

    public class TimerTriggerFunction
    {
        private readonly ILogger<TimerTriggerFunction> _logger;

        public TimerTriggerFunction(ILogger<TimerTriggerFunction> logger)
        {
            _logger = logger;
        }

        [FunctionName("TimerTriggerFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");
            // Add your logic here
            await Task.CompletedTask;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddHealthChecks();
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHealthChecks("/api/health");
                        });
                    });
                })
                .Build();

            host.Run();
        }
    }
}