using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Transport.RabbitMQ.Logging;
using Elders.Multithreading.Scheduler;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumer<T> : IConsumer<T>
    {
        static readonly ILog log = LogProvider.GetLogger(typeof(RabbitMqConsumer<>));

        private readonly string name;
        private readonly int numberOfWorkers;

        private readonly ISubscriptionMiddleware<T> subscriptions;
        private readonly List<WorkPool> pools;
        private readonly ISerializer serializer;
        private readonly IConnectionFactory connectionFactory;

        public RabbitMqConsumer(IConfiguration configuration, ISubscriptionMiddleware<T> subscriptions, ISerializer serializer, IConnectionFactory connectionFactory)
        {
            if (ReferenceEquals(null, subscriptions)) throw new ArgumentNullException(nameof(subscriptions));
            if (subscriptions.Subscribers.Any() == false) throw new ArgumentException("A consumer must have at least one subscriber to work properly.", nameof(subscriptions));
            if (ReferenceEquals(null, serializer)) throw new ArgumentNullException(nameof(serializer));

            name = typeof(T).Name;
            numberOfWorkers = configuration.GetValue<int>("cronus_transport_rabbimq_consumer_workerscount", 5);
            this.subscriptions = subscriptions;
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            pools = new List<WorkPool>();
        }

        protected virtual void ConsumerStart() { }
        protected virtual void ConsumerStarted() { }

        public IEnumerable<Func<RabbitMqContinuousConsumer<T>>> GetAvailableConsumers()
        {
            Dictionary<string, ISubscriptionMiddleware<T>> subscriptionsPerBC = new Dictionary<string, ISubscriptionMiddleware<T>>();

            foreach (var subscriber in subscriptions.Subscribers)
            {
                Type messageType = subscriber.GetInvolvedMessageTypes().First();
                var subscriberName = RabbitMqNamer.GetBoundedContext(messageType).ProductNamespace + "." + name;
                if (subscriptionsPerBC.TryGetValue(subscriberName, out ISubscriptionMiddleware<T> currentMiddleware))
                {
                    currentMiddleware.Subscribe(subscriber);
                }
                else
                {
                    var newMiddleware = new SubscriptionMiddleware<T>();
                    newMiddleware.Subscribe(subscriber);
                    subscriptionsPerBC.Add(subscriberName, newMiddleware);
                }
            }

            foreach (var item in subscriptionsPerBC)
            {
                yield return () => new RabbitMqContinuousConsumer<T>(item.Key, serializer, connectionFactory, item.Value);
            }
        }

        public void Start()
        {
            ConsumerStart();
            pools.Clear();

            foreach (var consumerfactory in GetAvailableConsumers())
            {
                var poolName = $"cronus: {typeof(T).Name}";
                WorkPool pool = new WorkPool(poolName, numberOfWorkers);
                for (int i = 0; i < numberOfWorkers; i++)
                {
                    pool.AddWork(consumerfactory());
                }
                pools.Add(pool);
                pool.StartCrawlers();
            }

            ConsumerStarted();
        }

        public void Stop()
        {
            pools.ForEach(pool => pool.Stop());
            pools.Clear();
        }
    }
}
