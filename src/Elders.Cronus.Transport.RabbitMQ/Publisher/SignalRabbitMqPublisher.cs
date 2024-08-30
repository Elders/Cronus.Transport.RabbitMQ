using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Publisher
{
    public sealed class SignalRabbitMqPublisher : PublisherBase<ISignal>
    {
        private readonly PublisherChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly RabbitMqOptions internalOptions;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly ILogger<SignalRabbitMqPublisher> logger;
        private readonly PublicRabbitMqOptionsCollection options;

        public SignalRabbitMqPublisher(IOptionsMonitor<BoundedContext> boundedContextOptionsMonitor, IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor, IOptionsMonitor<PublicRabbitMqOptionsCollection> optionsMonitor, IRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger, IEnumerable<DelegatingPublishHandler> handlers)
            : base(handlers)
        {
            this.boundedContext = boundedContextOptionsMonitor.CurrentValue;
            this.internalOptions = internalOptionsMonitor.CurrentValue;
            this.rabbitMqNamer = rabbitMqNamer;
            options = optionsMonitor.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.logger = logger;
        }

        protected override PublishResult PublishInternal(CronusMessage message)
        {
            PublishResult publishResult = PublishResult.Initial;

            Type messageType = null;

            try
            {
                string messageBC = message.BoundedContext;
                messageType = message.GetMessageType();
                IEnumerable<string> exchanges = rabbitMqNamer.Get_PublishTo_ExchangeNames(messageType);
                bool isInternalSignal = boundedContext.Name.Equals(messageBC, StringComparison.OrdinalIgnoreCase); // if the message will be published internally to the same BC

                if (isInternalSignal)
                {
                    foreach (var exchange in exchanges)
                    {
                        publishResult &= PublishInternally(message, messageBC, exchange, internalOptions);
                    }
                }
                else
                {
                    foreach (var exchange in exchanges)
                    {
                        publishResult &= PublishPublically(message, messageBC, exchange, options.PublicClustersOptions);
                    }
                }

                return publishResult;
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Unable to publish {messageType}"))
            {
                return PublishResult.Failed;
            }
        }

        private PublishResult PublishInternally(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions internalOptions)
        {
            IModel exchangeModel = channelResolver.Resolve(exchange, internalOptions, boundedContext);

            IBasicProperties props = exchangeModel.CreateBasicProperties();
            props = BuildMessageProperties(props, message);
            props = BuildInternalHeaders(props, message);

            return PublishUsingChannel(message, exchange, exchangeModel, props);
        }

        private PublishResult PublishPublically(CronusMessage message, string boundedContext, string exchange, IEnumerable<IRabbitMqOptions> scopedOptions)
        {
            PublishResult publishResult = PublishResult.Initial;

            foreach (var opt in scopedOptions)
            {
                IModel exchangeModel = channelResolver.Resolve(exchange, opt, boundedContext);

                IBasicProperties props = exchangeModel.CreateBasicProperties();
                props = BuildMessageProperties(props, message);
                props = BuildPublicHeaders(props, message);

                publishResult &= PublishUsingChannel(message, exchange, exchangeModel, props);
            }

            return publishResult;
        }

        private PublishResult PublishUsingChannel(CronusMessage message, string exchange, IModel exchangeModel, IBasicProperties properties)
        {
            try
            {
                byte[] body = serializer.SerializeToBytes(message);
                exchangeModel.BasicPublish(exchange, string.Empty, false, properties, body);

                logger.LogDebug("Published message to exchange {exchange} with headers {@headers}.", exchange, properties.Headers);

                return new PublishResult(true, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Published message to exchange {exchange} has FAILED.", exchange);

                return PublishResult.Failed;
            }
        }

        private IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string ttl = message.GetTtlMilliseconds();
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;
            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }

        private IBasicProperties BuildPublicHeaders(IBasicProperties properties, CronusMessage message)
        {
            string contractId = message.GetMessageType().GetContractId();
            string boundedContext = message.BoundedContext;
            string tenant = message.GetTenant();

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add($"{contractId}@{tenant}", boundedContext); // tenant specific binding in public signal
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());

            return properties;
        }

        private IBasicProperties BuildInternalHeaders(IBasicProperties properties, CronusMessage message)
        {
            string contractId = message.GetMessageType().GetContractId();
            string boundedContext = message.BoundedContext;

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add($"{contractId}", boundedContext); // dont use tenant in internal signal
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());

            return properties;
        }
    }
}
