namespace Elders.Cronus.Transport.RabbitMQ.Playground.Rpc.Responses
{
    public class Worker : BackgroundService
    {
        private readonly ICronusHost cronusHost;
        private readonly ILogger<Worker> _logger;

        public Worker(ICronusHost cronusHost, ILogger<Worker> logger)
        {
            this.cronusHost = cronusHost;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting service...");

            cronusHost.Start();


            _logger.LogInformation("Service started!");
        }
    }
}
