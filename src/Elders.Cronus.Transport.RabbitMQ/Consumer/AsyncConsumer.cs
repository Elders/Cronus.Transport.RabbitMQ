using System.Threading;
using System.Threading.Tasks;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    /// <summary>
    /// Transient secured consumer with some extra connection management.
    /// </summary>
    /// <typeparam name="TSubscriber">The subscriber type this consumer dispatches messages to.</typeparam>
    public class AsyncConsumer<TSubscriber> : AsyncConsumerBase<TSubscriber>
    {
        private readonly string queue;

        public AsyncConsumer(string queue, IChannel channel, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger)
            : base(channel, subscriberCollection, serializer, logger)
        {
            this.queue = queue;
        }

        /// <summary>
        /// Configures prefetch and registers this consumer with the broker.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous start.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await channel.BasicQosAsync(0, 1, false, cancellationToken).ConfigureAwait(false); // prefetch allow to avoid buffer of messages on the flight
            await channel.BasicConsumeAsync(queue, false, string.Empty, this, cancellationToken).ConfigureAwait(false); // we should use autoAck: false to avoid messages loosing

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Consumer for {cronus_subscriber} started.", typeof(TSubscriber).Name);
        }
    }
}
