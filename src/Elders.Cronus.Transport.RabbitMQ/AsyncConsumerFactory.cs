using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private readonly ConcurrentBag<AsyncConsumer<T>> consumers = new ConcurrentBag<AsyncConsumer<T>>();
        private bool isSystemQueue = false;
        private readonly string queueName;

        private IConnection connection;

        public AsyncConsumerFactory(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
            this.bcRabbitMqNamer = bcRabbitMqNamer;
            isSystemQueue = typeof(ISystemHandler).IsAssignableFrom(typeof(T));

            queueName = GetQueueName(this.boundedContext.Name, this.consumerOptions.FanoutMode);
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
                    model.ConfirmSelect();

                    if (i == 0)
                        RecoverModel(model);

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

        void RecoverModel(IModel model)
        {
            // exchangeName, dictionary<eventType,List<handlers>>
            var event2Handler = new Dictionary<string, Dictionary<string, List<string>>>();

            var routingHeaders = new Dictionary<string, object>();
            routingHeaders.Add("x-match", "any");

            foreach (var subscriber in subscriberCollection.Subscribers)
            {
                foreach (var msgType in subscriber.GetInvolvedMessageTypes().Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    string bc = msgType.GetBoundedContext(boundedContext.Name);
                    string messageContractId = msgType.GetContractId();
                    var exchangeNames = bcRabbitMqNamer.GetExchangeNames(msgType);

                    foreach (string exchangeName in exchangeNames)
                    {
                        Dictionary<string, List<string>> gg;
                        if (event2Handler.TryGetValue(exchangeName, out gg) == false)
                        {
                            gg = new Dictionary<string, List<string>>();
                            event2Handler.Add(exchangeName, gg);
                        }

                        List<string> handlers;
                        if (gg.TryGetValue(messageContractId, out handlers) == false)
                        {
                            handlers = new List<string>();
                            gg.Add(messageContractId, handlers);
                        }

                        handlers.Add(subscriber.Id);
                    }
                }
            }

            foreach (var subscriber in subscriberCollection.Subscribers)
            {
                foreach (var msgType in subscriber.GetInvolvedMessageTypes().Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    string bc = msgType.GetBoundedContext(boundedContext.Name);
                    string messageContractId = msgType.GetContractId();
                    string subscriberContractId = subscriber.Id;

                    if (routingHeaders.ContainsKey(messageContractId) == false)
                        routingHeaders.Add(messageContractId, bc);

                    string explicitHeader = $"{messageContractId}@{subscriberContractId}";
                    if (routingHeaders.ContainsKey(explicitHeader) == false)
                        routingHeaders.Add(explicitHeader, bc);
                }
            }

            model.QueueDeclare(queueName, true, false, false, routingHeaders);
            model.BasicQos(0, 10, false);

            var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Distinct().ToList();
            var exchangeGroups = messageTypes
                .SelectMany(mt => bcRabbitMqNamer.GetExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                .GroupBy(x => x.Exchange)
                .Distinct();
            foreach (var exchangeGroup in exchangeGroups)
            {
                // Standard exchange
                string standardExchangeName = exchangeGroup.Key;
                model.ExchangeDeclare(standardExchangeName, PipelineType.Headers.ToString(), true);

                // Scheduler exchange
                string schedulerExchangeName = $"{standardExchangeName}.Scheduler";
                var args = new Dictionary<string, object>();
                args.Add("x-delayed-type", PipelineType.Headers.ToString());
                model.ExchangeDeclare(schedulerExchangeName, "x-delayed-message", true, false, args);

                var bindHeaders = new Dictionary<string, object>();
                bindHeaders.Add("x-match", "any");

                foreach (Type msgType in exchangeGroup.Select(x => x.MessageType).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    bindHeaders.Add(msgType.GetContractId(), msgType.GetBoundedContext(boundedContext.Name));

                    var handlers = event2Handler[standardExchangeName][msgType.GetContractId()];
                    foreach (var handler in handlers)
                    {
                        bindHeaders.Add($"{msgType.GetContractId()}@{handler}", msgType.GetBoundedContext(boundedContext.Name));
                    }
                }
                model.QueueBind(queueName, standardExchangeName, string.Empty, bindHeaders);
                model.QueueBind(queueName, schedulerExchangeName, string.Empty, bindHeaders);

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

