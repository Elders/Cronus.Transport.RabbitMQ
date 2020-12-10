using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ
{
    [CronusStartup(Bootstraps.ExternalResource)]
    internal class PublishedLanguageStartup : ICronusStartup
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(PublishedLanguageStartup));
        private readonly RabbitMqInfrastructure infrastructure;

        public PublishedLanguageStartup(RabbitMqInfrastructure infrastructure)
        {
            this.infrastructure = infrastructure;
        }

        public void Bootstrap()
        {
            infrastructure.Initialize();
        }
    }
}
