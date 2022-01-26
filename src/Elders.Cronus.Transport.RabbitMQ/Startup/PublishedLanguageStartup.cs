namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    [CronusStartup(Bootstraps.ExternalResource)]
    internal class PublishedLanguageStartup : ICronusStartup
    {
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
