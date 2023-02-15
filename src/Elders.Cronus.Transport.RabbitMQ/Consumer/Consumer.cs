using System;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class Consumer<T> : IConsumer<T> where T : IMessageHandler
    {
        private readonly ILogger logger;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly ConsumerFactory<T> consumerFactory;

        public Consumer(IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, ISerializer serializer, ConsumerFactory<T> consumerFactory, ILogger<Consumer<T>> logger)
        {
            if (ReferenceEquals(null, subscriberCollection)) throw new ArgumentNullException(nameof(subscriberCollection));
            if (ReferenceEquals(null, serializer)) throw new ArgumentNullException(nameof(serializer));

            this.boundedContext = boundedContext.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.consumerFactory = consumerFactory;
            this.logger = logger;
        }

        public void Start()
        {
            try
            {
                if (subscriberCollection.Subscribers.Any() == false)
                {
                    logger.Warn(() => $"Consumer {boundedContext}.{typeof(T).Name} not started because there are no subscribers.");
                }

                consumerFactory.CreateAndStartConsumers();

            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to start rabbitmq consumer.")) { }
        }

        public void Stop()
        {
            consumerFactory.StopAsync().GetAwaiter().GetResult();
        }
    }
}
