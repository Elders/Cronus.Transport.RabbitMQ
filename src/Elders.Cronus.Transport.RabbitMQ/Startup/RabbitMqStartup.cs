using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.EventStore.Index;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Migrations;
using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    public abstract class RabbitMqStartup<T> : ICronusStartup
    {
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private readonly ILogger<RabbitMqStartup<T>> logger;

        private TenantsOptions tenantsOptions;
        private bool isSystemQueue = false;
        private readonly string queueName;

        public RabbitMqStartup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptionsMonitor, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<RabbitMqStartup<T>> logger)
        {
            this.tenantsOptions = tenantsOptionsMonitor.CurrentValue;
            this.boundedContext = boundedContext.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
            this.bcRabbitMqNamer = bcRabbitMqNamer;
            this.logger = logger;

            isSystemQueue = typeof(ISystemHandler).IsAssignableFrom(typeof(T));
            queueName = bcRabbitMqNamer.Get_QueueName(typeof(T), consumerOptions.CurrentValue.FanoutMode);

            tenantsOptionsMonitor.OnChange(newOptions =>
            {
                this.logger.Debug(() => "Cronus tenant options re-loaded with {@options}", newOptions);

                tenantsOptions = newOptions;

                using (var connection = connectionFactory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    RecoverModel(channel);
                }
            });
        }

        public void Bootstrap()
        {
            using (var connection = connectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                RecoverModel(channel);
            }
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
                .ToList();

            bool thereIsAScheduledQueue = false;
            string scheduledQueue = string.Empty;

            bool isTriggerQueue = typeof(T).Name.Equals(typeof(ITrigger).Name);
            bool isSagaQueue = typeof(T).Name.Equals(typeof(ISaga).Name) || typeof(T).Name.Equals(typeof(ISystemSaga).Name);
            if (isSagaQueue)
            {
                bool hasOneExchangeGroup = bindToExchangeGroups.Count == 1;
                if (hasOneExchangeGroup)
                {
                    var arguments = new Dictionary<string, object>()
                    {
                        { "x-dead-letter-exchange", bindToExchangeGroups[0].Key}
                    };

                    scheduledQueue = $"{queueName}.Scheduled";
                    model.QueueDeclare(scheduledQueue, true, false, false, arguments);

                    thereIsAScheduledQueue = true;
                }
                else if (bindToExchangeGroups.Count > 1)
                {
                    throw new Exception($"There are more than one exchanges defined for {typeof(T).Name}. RabbitMQ does not allow this functionality and you need to fix one or more of the following subscribers:{Environment.NewLine}{string.Join(Environment.NewLine, subscriberCollection.Subscribers.Select(sub => sub.Id))}");
                }
            }

            foreach (var exchangeGroup in bindToExchangeGroups)
            {
                // Standard exchange
                string standardExchangeName = exchangeGroup.Key;
                model.ExchangeDeclare(standardExchangeName, PipelineType.Headers.ToString(), true, false, null);

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

                if (thereIsAScheduledQueue)
                {
                    string deadLetterExchangeName = $"{standardExchangeName}.Delayer";
                    model.ExchangeDeclare(deadLetterExchangeName, ExchangeType.Headers, true, false);
                    model.QueueBind(scheduledQueue, deadLetterExchangeName, string.Empty, bindHeaders);
                }
            }
        }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class AppService_Startup : RabbitMqStartup<IApplicationService>
    {
        public AppService_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IApplicationService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<AppService_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class CronusEventStoreIndex_Startup : RabbitMqStartup<ICronusEventStoreIndex>
    {
        public CronusEventStoreIndex_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ICronusEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<CronusEventStoreIndex_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class EventStoreIndex_Startup : RabbitMqStartup<IEventStoreIndex>
    {
        public EventStoreIndex_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<EventStoreIndex_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Projection_Startup : RabbitMqStartup<IProjection>
    {
        public Projection_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<Projection_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Port_Startup : RabbitMqStartup<IPort>
    {
        public Port_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<Port_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Saga_Startup : RabbitMqStartup<ISaga>
    {
        public Saga_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<Saga_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Gateway_Startup : RabbitMqStartup<IGateway>
    {
        public Gateway_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IGateway> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<Gateway_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class TriggerPrivate_Startup : RabbitMqStartup<ITrigger>
    {
        public TriggerPrivate_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ITrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<TriggerPrivate_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemAppService_Startup : RabbitMqStartup<ISystemAppService>
    {
        public SystemAppService_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemAppService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<SystemAppService_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemSaga_Startup : RabbitMqStartup<ISystemSaga>
    {
        public SystemSaga_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemSaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<SystemSaga_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemPort_Startup : RabbitMqStartup<ISystemPort>
    {
        public SystemPort_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<SystemPort_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemTrigger_Startup : RabbitMqStartup<ISystemTrigger>
    {
        public SystemTrigger_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemTrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<SystemTrigger_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemProjection_Startup : RabbitMqStartup<ISystemProjection>
    {
        public SystemProjection_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<ISystemProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<SystemProjection_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class MigrationHandler_Startup : RabbitMqStartup<IMigrationHandler>
    {
        public MigrationHandler_Startup(IOptionsMonitor<TenantsOptions> tenantsOptions, IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISubscriberCollection<IMigrationHandler> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer, ILogger<MigrationHandler_Startup> logger) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer, logger) { }
    }
}

