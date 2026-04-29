using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    /// <summary>
    /// Initializes the RabbitMQ infrastructure required to host the published language exchanges.
    /// </summary>
    [CronusStartup(Bootstraps.ExternalResource)]
    public class PublishedLanguageStartup : ICronusStartup
    {
        private readonly RabbitMqInfrastructure infrastructure;

        public PublishedLanguageStartup(RabbitMqInfrastructure infrastructure)
        {
            ArgumentNullException.ThrowIfNull(infrastructure);

            this.infrastructure = infrastructure;
        }

        /// <summary>
        /// Bootstraps the RabbitMQ infrastructure for the published language.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous bootstrap.</param>
        public Task BootstrapAsync(CancellationToken cancellationToken = default)
        {
            infrastructure.Initialize();
            return Task.CompletedTask;
        }
    }
}
