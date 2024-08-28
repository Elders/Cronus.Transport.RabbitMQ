using Elders.Cronus.EventStore.Index;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Migrations;
using Elders.Cronus.Multitenancy;
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
        private readonly TenantsOptions tenantsOptions;
        private bool isSystemQueue = false;
        private readonly string queueName;

        public RabbitMqStartup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
            this.bcRabbitMqNamer = bcRabbitMqNamer;
            this.tenantsOptions = tenantsOptions.CurrentValue;

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

        private Dictionary<string, object> BuildQueueRoutingHeaders()
        {
            var routingHeaders = new Dictionary<string, object>();
            routingHeaders.Add("x-match", "any");

            foreach (var subscriber in subscriberCollection.Subscribers)
            {
                foreach (var msgType in subscriber.GetInvolvedMessageTypes().Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    string bc = msgType.GetBoundedContext(boundedContext.Name);

                    string messageContractId = msgType.GetContractId();
                    if (routingHeaders.ContainsKey(messageContractId) == false)
                        routingHeaders.Add(messageContractId, bc);

                    string handlerHeader = $"{messageContractId}@{subscriber.Id}";
                    if (routingHeaders.ContainsKey(handlerHeader) == false)
                        routingHeaders.Add(handlerHeader, bc);
                }
            }

            return routingHeaders;
        }

        private Dictionary<string, Dictionary<string, List<string>>> BuildEventToHandler()
        {
            // exchangeName, dictionary<eventType,List<handlers>>
            var event2Handler = new Dictionary<string, Dictionary<string, List<string>>>();

            foreach (ISubscriber subscriber in subscriberCollection.Subscribers)
            {
                foreach (Type msgType in subscriber.GetInvolvedMessageTypes().Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    string bc = msgType.GetBoundedContext(boundedContext.Name);
                    string messageContractId = msgType.GetContractId();
                    IEnumerable<string> exchangeNames = bcRabbitMqNamer.Get_BindTo_ExchangeNames(msgType);

                    foreach (string exchangeName in exchangeNames)
                    {
                        Dictionary<string, List<string>> message2Handlers;
                        if (event2Handler.TryGetValue(exchangeName, out message2Handlers) == false)
                        {
                            message2Handlers = new Dictionary<string, List<string>>();
                            event2Handler.Add(exchangeName, message2Handlers);
                        }

                        List<string> handlers;
                        if (message2Handlers.TryGetValue(messageContractId, out handlers) == false)
                        {
                            handlers = new List<string>();
                            message2Handlers.Add(messageContractId, handlers);
                        }

                        handlers.Add(subscriber.Id);
                    }
                }
            }

            return event2Handler;
        }
        private void RecoverModel(IModel model)
        {
            var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue).Distinct().ToList();

            var publishToExchangeGroups = messageTypes
                .SelectMany(mt => bcRabbitMqNamer.Get_PublishTo_ExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                .GroupBy(x => x.Exchange)
                .Distinct()
                .ToList();

            foreach (var publishExchangeGroup in publishToExchangeGroups)
            {
                model.ExchangeDeclare(publishExchangeGroup.Key, PipelineType.Headers.ToString(), true);
            }

            Dictionary<string, Dictionary<string, List<string>>> event2Handler = BuildEventToHandler();

            Dictionary<string, object> routingHeaders = BuildQueueRoutingHeaders();
            model.QueueDeclare(queueName, true, false, false, null);

            var bindToExchangeGroups = messageTypes
                .SelectMany(mt => bcRabbitMqNamer.Get_BindTo_ExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                .GroupBy(x => x.Exchange)
                .Distinct()
                .ToList(); // do tuk

            bool isTriggerQueue = typeof(T).Name.Equals(typeof(ITrigger).Name);
            bool isSagaQueue = typeof(T).Name.Equals(typeof(ISaga).Name) || typeof(T).Name.Equals(typeof(ISystemSaga).Name);

            foreach (var exchangeGroup in bindToExchangeGroups)
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

                foreach (Type msgType in exchangeGroup.Select(x => x.MessageType))
                {
                    string contractId = msgType.GetContractId();
                    string bc = msgType.GetBoundedContext(boundedContext.Name);

                    if (bc.Equals(boundedContext.Name, StringComparison.OrdinalIgnoreCase) == false && isSystemQueue)
                        throw new Exception($"The message {msgType.Name} has a bounded context {bc} which is different than the configured {boundedContext.Name}.");

                    if (bc.Equals(boundedContext.Name, StringComparison.OrdinalIgnoreCase) == false || isTriggerQueue)
                    {
                        bindHeaders.Add(contractId, bc);

                        var backwardCompHandlers = event2Handler[standardExchangeName][contractId];
                        foreach (var handler in backwardCompHandlers)
                        {
                            bindHeaders.Add($"{contractId}@{handler}", bc);
                        }

                        foreach (string tenant in tenantsOptions.Tenants)
                        {
                            string contractIdWithTenant = $"{contractId}@{tenant}";
                            bindHeaders.Add(contractIdWithTenant, bc);

                            var handlers = event2Handler[standardExchangeName][contractId];
                            foreach (var handler in handlers)
                            {
                                string key = $"{contractId}@{handler}@{tenant}";
                                bindHeaders.Add(key, bc);
                            }
                        }
                    }
                    else
                    {
                        bindHeaders.Add(contractId, bc);

                        var handlers = event2Handler[standardExchangeName][contractId];
                        foreach (var handler in handlers)
                        {
                            string key = $"{contractId}@{handler}";
                            bindHeaders.Add(key, bc);
                        }
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
        public AppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IApplicationService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class CronusEventStoreIndex_Startup : RabbitMqStartup<ICronusEventStoreIndex>
    {
        public CronusEventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ICronusEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class EventStoreIndex_Startup : RabbitMqStartup<IEventStoreIndex>
    {
        public EventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Projection_Startup : RabbitMqStartup<IProjection>
    {
        public Projection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Port_Startup : RabbitMqStartup<IPort>
    {
        public Port_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Saga_Startup : RabbitMqStartup<ISaga>
    {
        public Saga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Gateway_Startup : RabbitMqStartup<IGateway>
    {
        public Gateway_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IGateway> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Trigger_Startup : RabbitMqStartup<ITrigger>
    {
        public Trigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ITrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemAppService_Startup : RabbitMqStartup<ISystemAppService>
    {
        public SystemAppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemAppService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemSaga_Startup : RabbitMqStartup<ISystemSaga>
    {
        public SystemSaga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemSaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemPort_Startup : RabbitMqStartup<ISystemPort>
    {
        public SystemPort_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemTrigger_Startup : RabbitMqStartup<ISystemTrigger>
    {
        public SystemTrigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemTrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemProjection_Startup : RabbitMqStartup<ISystemProjection>
    {
        public SystemProjection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class MigrationHandler_Startup : RabbitMqStartup<IMigrationHandler>
    {
        public MigrationHandler_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IMigrationHandler> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, IOptionsMonitor<TenantsOptions> tenantsOptions) : base(consumerOptions, boundedContext, subscriberCollection, connectionFactory, bcRabbitMqNamer, tenantsOptions) { }
    }
}

