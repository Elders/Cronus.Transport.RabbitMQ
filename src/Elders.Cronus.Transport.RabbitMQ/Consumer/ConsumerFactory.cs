using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConsumerFactory<T>
    {
        private readonly ILogger<ConsumerFactory<T>> logger;
        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly ConcurrentBag<AsyncConsumerBase> consumers = new ConcurrentBag<AsyncConsumerBase>();
        private readonly RabbitMqOptions options;
        private string queueName;

        public ConsumerFactory(IOptionsMonitor<RabbitMqOptions> optionsMonitor, ConsumerPerQueueChannelResolver channelResolver, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, ILogger<ConsumerFactory<T>> logger)
        {
            this.logger = logger;
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
            bool isTrigger = typeof(T).IsAssignableFrom(typeof(ITrigger));

            for (int i = 0; i < consumerOptions.WorkersCount; i++)
            {
                IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);
                string consumerChannelKey = $"{boundedContext.Name}_{typeof(T).Name}_{i}";
                IModel channel = channelResolver.Resolve(consumerChannelKey, scopedOptions, options.VHost);

                AsyncConsumerBase<T> asyncListener = isTrigger == true ?
                    new AsyncSignalConsumer<T>(queueName, channel, subscriberCollection, serializer, logger) :
                    new AsyncConsumer<T>(queueName, channel, subscriberCollection, serializer, logger);

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
                return $"{boundedContext}.{systemMarker}{typeof(T).Name}";
            }
        }
    }
}
