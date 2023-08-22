using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class Consumer<T> : IConsumer<T> where T : IMessageHandler
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
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

        public Task StartAsync()
        {
            try
            {
                if (subscriberCollection.Subscribers.Any() == false)
                {
                    logger.Info(() => $"Consumer {boundedContext}.{typeof(T).Name} not started because there are no subscribers.");
                }

                consumerFactory.CreateAndStartConsumers(tokenSource.Token);

            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to start rabbitmq consumer.")) { }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            tokenSource.Cancel();
            return consumerFactory.StopAsync();
        }
    }
}
