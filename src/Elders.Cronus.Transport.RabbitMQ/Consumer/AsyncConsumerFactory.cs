using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncConsumerFactory<T>
    {
        private readonly ILogger logger = CronusLogger.CreateLogger(typeof(AsyncConsumerFactory<>));
        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly ConcurrentBag<AsyncConsumer<T>> consumers = new ConcurrentBag<AsyncConsumer<T>>();
        private readonly string queueName;
        private readonly RabbitMqOptions options;

        public AsyncConsumerFactory(IOptionsMonitor<RabbitMqOptions> optionsMonitor, ConsumerPerQueueChannelResolver channelResolver, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.options = optionsMonitor.CurrentValue;

            queueName = GetQueueName(this.boundedContext.Name, this.consumerOptions.FanoutMode);
        }

        public void CreateAndStartConsumers()
        {
            for (int i = 0; i < consumerOptions.WorkersCount; i++)
            {
                string consumerChannelKey = $"{boundedContext.Name}_{typeof(T).Name}_{i}";
                IModel channel = channelResolver.Resolve(consumerChannelKey, options, options.VHost);

                AsyncConsumer<T> asyncListener = new AsyncConsumer<T>(queueName, channel, subscriberCollection, serializer, logger);
                consumers.Add(asyncListener);
            }
        }

        public async Task StopAsync()
        {
            IEnumerable<Task> stopTasks = consumers.Select(consumer => consumer.StopAsync());

            await Task.WhenAll(stopTasks).ConfigureAwait(false);
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

