using Elders.Cronus.EventStore.Index;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Migrations;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    public abstract class RabbitMqStartup<T> : ICronusStartup
    {
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private bool isSystemQueue = false;
        private readonly string queueName;

        public RabbitMqStartup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
            this.bcRabbitMqNamer = bcRabbitMqNamer;

            isSystemQueue = typeof(ISystemHandler).IsAssignableFrom(typeof(T));
            queueName = GetQueueName(this.boundedContext.Name, this.consumerOptions.FanoutMode);
        }

        public void Bootstrap()
        {
            using (var connection = connectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                RecoverModel(channel);
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

        private void RecoverModel(IModel model)
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
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class AppService_Startup : RabbitMqStartup<IApplicationService>
    {
        public AppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IApplicationService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class CronusEventStoreIndex_Startup : RabbitMqStartup<ICronusEventStoreIndex>
    {
        public CronusEventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ICronusEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class EventStoreIndex_Startup : RabbitMqStartup<IEventStoreIndex>
    {
        public EventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Projection_Startup : RabbitMqStartup<IProjection>
    {
        public Projection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Port_Startup : RabbitMqStartup<IPort>
    {
        public Port_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Saga_Startup : RabbitMqStartup<ISaga>
    {
        public Saga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Gateway_Startup : RabbitMqStartup<IGateway>
    {
        public Gateway_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IGateway> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Trigger_Startup : RabbitMqStartup<ITrigger>
    {
        public Trigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ITrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemAppService_Startup : RabbitMqStartup<ISystemAppService>
    {
        public SystemAppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemAppService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemSaga_Startup : RabbitMqStartup<ISystemSaga>
    {
        public SystemSaga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemSaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemPort_Startup : RabbitMqStartup<ISystemPort>
    {
        public SystemPort_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemTrigger_Startup : RabbitMqStartup<ISystemTrigger>
    {
        public SystemTrigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemTrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemProjection_Startup : RabbitMqStartup<ISystemProjection>
    {
        public SystemProjection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class MigrationHandler_Startup : RabbitMqStartup<IMigrationHandler>
    {
        public MigrationHandler_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IMigrationHandler> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }
}

