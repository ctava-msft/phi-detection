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
            try
            {

                var host = new HostBuilder()
                    .ConfigureFunctionsWorkerDefaults()
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Debug);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddHealthChecks();
                        //services.AddTimers();
                    })
                    .Build();

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

        public TimerTriggerFunction(ILogger<TimerTriggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("TimerTriggerFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            try
            {
                //_logger.LogInformation($"Timer trigger function executed at: {DateTime.Now}");
                _logger.LogInformation($"Timer trigger function executed at: {myTimer.ScheduleStatus.Last}");
                // Add your logic here
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the timer trigger function.");
                throw;
            }
        }
    }
}