using System;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Multithreading.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumer<T> : IConsumer<T> where T : IMessageHandler
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConsumer<>));

        private RabbitMqConsumerOptions options;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly ISerializer serializer;
        private readonly IConnectionFactory connectionFactory;
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private readonly WorkPoolFactory workPoolFactory;
        private WorkPool pool;

        public RabbitMqConsumer(IOptionsMonitor<RabbitMqConsumerOptions> options, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, ISerializer serializer, IConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, WorkPoolFactory workPoolFactory)
        {
            if (ReferenceEquals(null, subscriberCollection)) throw new ArgumentNullException(nameof(subscriberCollection));
            if (ReferenceEquals(null, serializer)) throw new ArgumentNullException(nameof(serializer));

            this.boundedContext = boundedContext.CurrentValue;
            this.options = options.CurrentValue;
            options.OnChange(OptionsChanged);
            this.subscriberCollection = subscriberCollection;
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            this.bcRabbitMqNamer = bcRabbitMqNamer;
            this.workPoolFactory = workPoolFactory;
        }

        protected virtual void ConsumerStart() { }
        protected virtual void ConsumerStarted() { }

        public void Start()
        {
            if (subscriberCollection.Subscribers.Any() == false)
            {
                logger.Warn(() => $"Consumer {boundedContext}.{typeof(T).Name} not started because there are no subscribers.");
                return;
            }

            ConsumerStart();

            if (pool is null == false)
            {
                logger.Warn(() => $"RabbitMq consumer has already been started with '{options.WorkersCount}' consumenrs. Returning.");
                return;
            }

            var poolName = $"cronus_{typeof(T).Name}";
            pool = workPoolFactory.Create(poolName, options.WorkersCount);
            for (int i = 0; i < options.WorkersCount; i++)
            {
                var consumer = new RabbitMqContinuousConsumer<T>(boundedContext, serializer, connectionFactory, subscriberCollection, bcRabbitMqNamer, options.FanoutMode);
                pool.AddWork(consumer);
            }

            pool.StartCrawlers();

            ConsumerStarted();
        }

        public void Stop()
        {
            pool?.Stop();
        }

        private void OptionsChanged(RabbitMqConsumerOptions options)
        {
            if (this.options == options)
                return;

            logger.Debug(() => "RabbitMqConsumerOptions changed from {@CurrentOptions} to {@NewOptions}.", this.options, options);

            this.options = options;

            Stop();
            Start();
        }
    }
}
