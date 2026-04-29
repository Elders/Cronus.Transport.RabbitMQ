using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Elders.Cronus.Transport.RabbitMQ.Startup;
using System.Threading;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConsumerFactory<T>
    {
        private readonly ILogger<ConsumerFactory<T>> logger;
        private readonly TypeContainer<ISaga> allSagas;
        private readonly ConsumerPerQueueChannelResolver channelResolver;
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly SchedulePoker<T> schedulePoker;
        private readonly ConcurrentBag<AsyncConsumerBase> consumers = new ConcurrentBag<AsyncConsumerBase>();
        private readonly RabbitMqOptions options;
        private string queueName;

        public ConsumerFactory(TypeContainer<ISaga> allSagas, IOptionsMonitor<RabbitMqOptions> optionsMonitor, ConsumerPerQueueChannelResolver channelResolver, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, SchedulePoker<T> schedulePoker, ILogger<ConsumerFactory<T>> logger)
        {
            this.logger = logger;
            this.boundedContext = boundedContext.CurrentValue;
            this.allSagas = allSagas;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.schedulePoker = schedulePoker;
            this.options = optionsMonitor.CurrentValue;

            queueName = GetQueueName(this.boundedContext.Name, this.consumerOptions.FanoutMode);
        }

        /// <summary>
        /// Creates and starts the configured number of RabbitMQ consumers for the subscriber type.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous start.</param>
        public async Task CreateAndStartConsumersAsync(CancellationToken cancellationToken)
        {
            bool isTrigger = typeof(T).IsAssignableFrom(typeof(ITrigger));

            if (isTrigger)
                await CreateAndStartTriggerConsumersAsync(cancellationToken).ConfigureAwait(false);
            else
                await CreateAndStartNormalConsumersAsync(cancellationToken).ConfigureAwait(false);

            CreateAndStartSchedulePoker(cancellationToken);
        }

        private async Task CreateAndStartTriggerConsumersAsync(CancellationToken cancellationToken)
        {
            IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext.Name);

            for (int i = 0; i < consumerOptions.WorkersCount; i++)
            {
                string consumerChannelKey = $"{boundedContext.Name}_{typeof(T).Name}_{i}";
                IChannel channel = await channelResolver.ResolveAsync(consumerChannelKey, scopedOptions, options.VHost, cancellationToken).ConfigureAwait(false);

                AsyncConsumer<T> asyncListener = new AsyncConsumer<T>(queueName, channel, subscriberCollection, serializer, logger);
                await asyncListener.StartAsync(cancellationToken).ConfigureAwait(false);

                consumers.Add(asyncListener);
            }
        }

        private async Task CreateAndStartNormalConsumersAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < consumerOptions.WorkersCount; i++)
            {
                string consumerChannelKey = $"{boundedContext.Name}_{typeof(T).Name}_{i}";
                IChannel channel = await channelResolver.ResolveAsync(consumerChannelKey, options, options.VHost, cancellationToken).ConfigureAwait(false);

                AsyncConsumer<T> asyncListener = new AsyncConsumer<T>(queueName, channel, subscriberCollection, serializer, logger);
                await asyncListener.StartAsync(cancellationToken).ConfigureAwait(false);

                consumers.Add(asyncListener);
            }
        }

        private void CreateAndStartSchedulePoker(CancellationToken cancellationToken)
        {
            bool isSaga = typeof(ISaga).IsAssignableFrom(typeof(T));
            if (isSaga)
            {
                bool isSystemSaga = typeof(ISystemSaga).IsAssignableFrom(typeof(T));
                bool hasRegisteredSagas = allSagas.Items.Where(saga => typeof(ISystemSaga).IsAssignableFrom(saga) == isSystemSaga).Any();
                if (hasRegisteredSagas)
                {
                    _ = schedulePoker.PokeAsync(cancellationToken);
                }
            }
        }

        public async Task StopAsync()
        {
            IEnumerable<Task> stopTasks = consumers.Select(consumer => consumer.StopAsync());

            await Task.WhenAll(stopTasks).ConfigureAwait(false);

            subscriberCollection.UnsubscribeAll();
            consumers.Clear();
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
