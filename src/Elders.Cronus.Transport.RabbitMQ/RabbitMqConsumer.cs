using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumer<T> : IConsumer<T> where T : IMessageHandler
    {
        private readonly ILogger logger;

        private RabbitMqConsumerOptions options;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly AsyncConsumerFactory<T> consumerFactory;

        public RabbitMqConsumer(IOptionsMonitor<RabbitMqConsumerOptions> options, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, ISerializer serializer, AsyncConsumerFactory<T> consumerFactory, ILogger<RabbitMqConsumer<T>> logger)
        {
            if (ReferenceEquals(null, subscriberCollection)) throw new ArgumentNullException(nameof(subscriberCollection));
            if (ReferenceEquals(null, serializer)) throw new ArgumentNullException(nameof(serializer));

            this.boundedContext = boundedContext.CurrentValue;
            this.options = options.CurrentValue;
            options.OnChange(OptionsChanged);
            this.subscriberCollection = subscriberCollection;
            this.consumerFactory = consumerFactory;
            this.logger = logger;
        }

        protected virtual void ConsumerStart() { }
        protected virtual void ConsumerStarted() { }

        public Task StartAsync()
        {
            try
            {
                if (subscriberCollection.Subscribers.Any() == false)
                {
                    logger.Warn(() => $"Consumer {boundedContext}.{typeof(T).Name} not started because there are no subscribers.");
                    return Task.CompletedTask;
                }

                return consumerFactory.CreateConsumersAsync();

            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to start rabbitmq consumer."))
            {
                return Task.FromException(ex);
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await consumerFactory.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to stop rabbitmq consumer.")) { }
        }

        private void OptionsChanged(RabbitMqConsumerOptions options)
        {
            if (this.options == options)
                return;

            logger.Debug(() => "RabbitMqConsumerOptions changed from {@CurrentOptions} to {@NewOptions}.", this.options, options);

            this.options = options;

            StopAsync().GetAwaiter().GetResult();
            StartAsync().GetAwaiter().GetResult();
        }

        public void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
