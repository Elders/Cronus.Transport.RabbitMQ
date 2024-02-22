using Elders.Cronus.EventStore.Index;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Migrations;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Elders.Cronus.Multitenancy;
using System.Reflection;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    public class SchedulePoker<T> //where T : IMessageHandler
    {
        private readonly IOptionsMonitor<RabbitMqOptions> rmqOptionsMonitor;
        private readonly ConnectionResolver connectionResolver;
        private readonly IOptionsMonitor<BoundedContext> boundedContext;
        AsyncEventingBasicConsumer consumer;

        public SchedulePoker(IOptionsMonitor<RabbitMqOptions> rmqOptionsMonitor, ConnectionResolver connectionResolver, IOptionsMonitor<BoundedContext> boundedContext)
        {
            this.rmqOptionsMonitor = rmqOptionsMonitor;
            this.connectionResolver = connectionResolver;
            this.boundedContext = boundedContext;
        }

        public async Task PokeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string queueName = $"{GetQueueName(boundedContext.CurrentValue.Name)}.Scheduled";

                while (cancellationToken.IsCancellationRequested == false)
                {
                    IConnection connection = connectionResolver.Resolve(queueName, rmqOptionsMonitor.CurrentValue);

                    using (IModel channel = connection.CreateModel())
                    {
                        try
                        {
                            consumer = new AsyncEventingBasicConsumer(channel);
                            consumer.Received += AsyncListener_Received;

                            string consumerTag = channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

                            await Task.Delay(30000, cancellationToken).ConfigureAwait(false);

                            consumer.Received -= AsyncListener_Received;
                        }
                        catch (Exception)
                        {
                            await Task.Delay(5000).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            return Task.CompletedTask;
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

    public abstract class RabbitMqStartup<T> : ICronusStartup
    {
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly TenantsOptions tenantsOptions;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private bool isSystemQueue = false;
        private readonly string queueName;

        public RabbitMqStartup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsMonitor, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.tenantsOptions = tenantsMonitor.CurrentValue;
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
                    IEnumerable<string> exchangeNames = bcRabbitMqNamer.GetExchangeNames(msgType);

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

                    if (bc != boundedContext.Name)
                        throw new Exception($"The message {msgType.Name} has a bounded context {bc} which is different than the configured {boundedContext.Name}.");

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

        private void RecoverQueues(IModel model)
        {
            Dictionary<string, object> routingHeaders = BuildQueueRoutingHeaders();

            // Recover standard queue
            model.QueueDeclare(queueName, true, false, false, routingHeaders);

            // Recover scheduled queue
            bool isSagaQueue = typeof(T).Name.Equals(typeof(ISaga).Name) || typeof(T).Name.Equals(typeof(ISystemSaga).Name);
        }

        private void RecoverModel(IModel model)
        {
            Dictionary<string, Dictionary<string, List<string>>> event2Handler = BuildEventToHandler();

            Dictionary<string, object> routingHeaders = BuildQueueRoutingHeaders();
            model.QueueDeclare(queueName, true, false, false, routingHeaders);

            var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue).Distinct().ToList();
            var exchangeGroups = messageTypes
                .SelectMany(mt => bcRabbitMqNamer.GetExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                .GroupBy(x => x.Exchange)
                .Distinct()
                .ToList();

            bool thereIsAScheduledQueue = false;
            string scheduledQueue = string.Empty;

            bool isSagaQueue = typeof(T).Name.Equals(typeof(ISaga).Name) || typeof(T).Name.Equals(typeof(ISystemSaga).Name);
            if (isSagaQueue)
            {
                bool hasOneExchangeGroup = exchangeGroups.Count == 1;
                if (hasOneExchangeGroup)
                {
                    routingHeaders.Add("x-dead-letter-exchange", exchangeGroups[0].Key);

                    scheduledQueue = $"{queueName}.Scheduled";
                    model.QueueDeclare(scheduledQueue, true, false, false, routingHeaders);

                    thereIsAScheduledQueue = true;
                }
                else if (exchangeGroups.Count > 1)
                {
                    throw new Exception($"There are more than one exchanges defined for {typeof(T).Name}. RabbitMQ does not allow this functionality and you need to fix one or more of the following subscribers:{Environment.NewLine}{string.Join(Environment.NewLine, subscriberCollection.Subscribers.Select(sub => sub.Id))}");
                }
            }

            foreach (var exchangeGroup in exchangeGroups)
            {
                // Standard exchange
                string standardExchangeName = exchangeGroup.Key;
                model.ExchangeDeclare(standardExchangeName, PipelineType.Headers.ToString(), true);

                var bindHeaders = new Dictionary<string, object>();
                bindHeaders.Add("x-match", "any");

                foreach (Type msgType in exchangeGroup.Select(x => x.MessageType).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                {
                    bindHeaders.Add(msgType.GetContractId(), msgType.GetBoundedContext(boundedContext.Name));

                    var handlers = event2Handler[standardExchangeName][msgType.GetContractId()];
                    foreach (var handler in handlers)
                    {
                        string key = $"{msgType.GetContractId()}@{handler}";
                        if (bindHeaders.ContainsKey(key) == false)
                            bindHeaders.Add(key, msgType.GetBoundedContext(boundedContext.Name));
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

    public abstract class PublicRabbitMqStartup<T> : ICronusStartup
    {
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly TenantsOptions tenantsOptions;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
        private bool isSystemQueue = false;
        private readonly string queueName;

        public PublicRabbitMqStartup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsMonitor, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.tenantsOptions = tenantsMonitor.CurrentValue;
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
                    IEnumerable<string> exchangeNames = bcRabbitMqNamer.GetExchangeNames(msgType);

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
            model.QueueDeclare(queueName, true, false, false, null);

            Dictionary<string, Dictionary<string, List<string>>> event2Handler = BuildEventToHandler();

            var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue).Distinct().ToList();
            var exchangeGroups = messageTypes
                .SelectMany(mt => bcRabbitMqNamer.GetExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                .Distinct()
                .GroupBy(x => x.Exchange)
                .ToList();


            bool thereIsAScheduledQueue = false;
            string scheduledQueue = string.Empty;

            bool isTriggerQueue = typeof(T).Name.Equals(typeof(ITrigger).Name);
            bool isSagaQueue = typeof(T).Name.Equals(typeof(ISaga).Name) || typeof(T).Name.Equals(typeof(ISystemSaga).Name);
            if (isSagaQueue)
            {
                bool hasOneExchangeGroup = exchangeGroups.Count == 1;
                if (hasOneExchangeGroup)
                {
                    var arguments = new Dictionary<string, object>()
                    {
                        { "x-dead-letter-exchange", exchangeGroups[0].Key}
                    };

                    scheduledQueue = $"{queueName}.Scheduled";
                    model.QueueDeclare(scheduledQueue, true, false, false, arguments);

                    thereIsAScheduledQueue = true;
                }
                else
                {
                    throw new Exception($"There are more than one exchanges defined for {typeof(T).Name}. To support delayed message feature you need to fix one or more of the following subscribers:{Environment.NewLine}{string.Join(Environment.NewLine, subscriberCollection.Subscribers.Select(sub => sub.Id))}");
                }
            }

            foreach (var exchangeGroup in exchangeGroups)
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

                    if (bc != boundedContext.Name && isSystemQueue)
                        throw new Exception($"The message {msgType.Name} has a bounded context {bc} which is different than the configured {boundedContext.Name}.");

                    if (bc != boundedContext.Name || isTriggerQueue)
                    {
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
        public AppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IApplicationService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class CronusEventStoreIndex_Startup : RabbitMqStartup<ICronusEventStoreIndex>
    {
        public CronusEventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ICronusEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class EventStoreIndex_Startup : RabbitMqStartup<IEventStoreIndex>
    {
        public EventStoreIndex_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IEventStoreIndex> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Projection_Startup : RabbitMqStartup<IProjection>
    {
        public Projection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Port_Startup : PublicRabbitMqStartup<IPort>
    {
        public Port_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Saga_Startup : RabbitMqStartup<ISaga>
    {
        public Saga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Gateway_Startup : RabbitMqStartup<IGateway>
    {
        public Gateway_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IGateway> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class Trigger_Startup : PublicRabbitMqStartup<ITrigger>
    {
        public Trigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ITrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemAppService_Startup : RabbitMqStartup<ISystemAppService>
    {
        public SystemAppService_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISystemAppService> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemSaga_Startup : RabbitMqStartup<ISystemSaga>
    {
        public SystemSaga_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISystemSaga> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemPort_Startup : RabbitMqStartup<ISystemPort>
    {
        public SystemPort_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISystemPort> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemTrigger_Startup : RabbitMqStartup<ISystemTrigger>
    {
        public SystemTrigger_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISystemTrigger> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class SystemProjection_Startup : RabbitMqStartup<ISystemProjection>
    {
        public SystemProjection_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<ISystemProjection> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }

    [CronusStartup(Bootstraps.Configuration)]
    public class MigrationHandler_Startup : RabbitMqStartup<IMigrationHandler>
    {
        public MigrationHandler_Startup(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<TenantsOptions> tenantsOptions, ISubscriberCollection<IMigrationHandler> subscriberCollection, IRabbitMqConnectionFactory connectionFactory, BoundedContextRabbitMqNamer bcRabbitMqNamer) : base(consumerOptions, boundedContext, tenantsOptions, subscriberCollection, connectionFactory, bcRabbitMqNamer) { }
    }
}

