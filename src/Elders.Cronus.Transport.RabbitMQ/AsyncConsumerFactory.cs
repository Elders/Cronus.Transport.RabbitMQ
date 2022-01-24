using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncConsumerFactory<T>
    {
        private readonly ILogger logger = CronusLogger.CreateLogger(typeof(AsyncConsumerFactory<>));
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly ConcurrentBag<AsyncConsumer<T>> consumers = new ConcurrentBag<AsyncConsumer<T>>();

        private IConnection connection;

        public AsyncConsumerFactory(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
        }

        public Task CreateConsumersAsync()
        {
            connection = connectionFactory.CreateConnection();

            string queue = GetQueueName(boundedContext.Name);

            if (connection.IsOpen)
            {
                for (int i = 0; i < consumerOptions.WorkersCount; i++)
                {
                    IModel model = connection.CreateModel();
                    AsyncConsumer<T> asyncListener = new AsyncConsumer<T>(queue, model, subscriberCollection, serializer, logger);
                    consumers.Add(asyncListener);
                }
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            foreach (var consumer in consumers)
            {
                await consumer.StopAsync();
            }

            if (connection is not null && connection.IsOpen)
            {
                connection.Close();
            }
        }

        private string GetQueueName(string boundedContext, bool useFanoutMode = false)
        {
            if (useFanoutMode)
            {
                return $"{boundedContext}.{typeof(T).Name}.{Environment.MachineName}";
            }
            else
            {
                string systemMarker = typeof(ISystemHandler).IsAssignableFrom(typeof(T)) ? "cronus." : string.Empty;
                // This is the default
                return $"{boundedContext}.{systemMarker}{typeof(T).Name}";
            }
        }

    }
}

