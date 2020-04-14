using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Multithreading.Scheduler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumer<T> : IConsumer<T>
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConsumer<>));

        private readonly BoundedContext boundedContext;
        private readonly int numberOfWorkers;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly List<WorkPool> pools;
        private readonly ISerializer serializer;
        private readonly IConnectionFactory connectionFactory;

        public RabbitMqConsumer(IConfiguration configuration, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, ISerializer serializer, IConnectionFactory connectionFactory)
        {
            if (ReferenceEquals(null, subscriberCollection)) throw new ArgumentNullException(nameof(subscriberCollection));
            if (ReferenceEquals(null, serializer)) throw new ArgumentNullException(nameof(serializer));

            this.boundedContext = boundedContext.CurrentValue;
            numberOfWorkers = configuration.GetValue<int>("cronus_transport_rabbimq_consumer_workerscount", 5);
            this.subscriberCollection = subscriberCollection;
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            pools = new List<WorkPool>();
        }

        protected virtual void ConsumerStart() { }
        protected virtual void ConsumerStarted() { }

        public void Start()
        {
            if (subscriberCollection.Subscribers.Any() == false)
            {
                logger.Warn($"Consumer {boundedContext}.{typeof(T).Name} not started because there are no subscribers");
                return;
            }

            ConsumerStart();
            pools.Clear();

            var poolName = $"cronus: {typeof(T).Name}";
            WorkPool pool = new WorkPool(poolName, numberOfWorkers);
            for (int i = 0; i < numberOfWorkers; i++)
            {
                var consumer = new RabbitMqContinuousConsumer<T>(boundedContext, serializer, connectionFactory, subscriberCollection);
                pool.AddWork(consumer);
            }
            pools.Add(pool);
            pool.StartCrawlers();


            ConsumerStarted();
        }

        public void Stop()
        {
            pools?.ForEach(pool => pool.Stop());
            pools?.Clear();
        }
    }
}
