namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    [CronusStartup(Bootstraps.ExternalResource)]
    public class PublishedLanguageStartup : ICronusStartup
    {
        private readonly RabbitMqInfrastructure infrastructure;
        private readonly BoundedContext boundedContext;

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
