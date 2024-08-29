using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ.Publisher
{
    public sealed class SignalRabbitMqPublisher : PublisherBase<ISignal>
    {
        private readonly PublisherChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly ILogger<SignalRabbitMqPublisher> logger;
        private readonly PublicRabbitMqOptions publicOptions;
        private readonly RabbitMqOptions internalOptions;

        public SignalRabbitMqPublisher(ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<RabbitMqOptions> internalOptions, IOptionsMonitor<PublicRabbitMqOptions> publicOptions, IRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger)
            : base(tenantResolver, boundedContext.CurrentValue, logger)
        {
            this.rabbitMqNamer = rabbitMqNamer;
            this.boundedContext = boundedContext.CurrentValue;
            this.publicOptions = publicOptions.CurrentValue;
            this.internalOptions = internalOptions.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.logger = logger;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            Type messageType = null;
            bool isSuccess = false;
            try
            {
                string messageBC = message.BoundedContext;
                messageType = message.Payload.GetType();
                IEnumerable<string> exchanges = rabbitMqNamer.Get_PublishTo_ExchangeNames(messageType);
                bool isInternalSignal = boundedContext.Name.Equals(messageBC, StringComparison.OrdinalIgnoreCase); // if the message will be published internally to the same BC

                if (isInternalSignal)
                {
                    foreach (var exchange in exchanges)
                    {
                        isSuccess &= PublishInternally(message, messageBC, exchange, internalOptions);
                    }
                }
                else
                {
                    foreach (var exchange in exchanges)
                    {
                        isSuccess &= PublishPublically(message, messageBC, exchange, publicOptions);
                    }
                }

                return isSuccess;
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Unable to publish {messageType}"))
            {
                return false;
            }
        }

        private bool PublishInternally(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions internalOptions)
        {
            IModel exchangeModel = channelResolver.Resolve(exchange, internalOptions, boundedContext);
            IBasicProperties props = exchangeModel.CreateBasicProperties();
            props = BuildMessageProperties(props, message);

            return PublishUsingChannel(message, exchange, exchangeModel, props);
        }

        private bool PublishPublically(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions scopedOptions)
        {
            bool isPublished = false;
            IBasicProperties props = null;

            IModel exchangeModel = channelResolver.Resolve(exchange, scopedOptions, boundedContext);

            if (props == null)
            {
                props = exchangeModel.CreateBasicProperties();
                props = BuildMessageProperties(props, message);
            }
            isPublished &= PublishUsingChannel(message, exchange, exchangeModel, props);

            return isPublished;
        }

        private bool PublishUsingChannel(CronusMessage message, string exchange, IModel exchangeModel, IBasicProperties properties)
        {
            try
            {
                byte[] body = serializer.SerializeToBytes(message);
                exchangeModel.BasicPublish(exchange, string.Empty, false, properties, body);

                logger.LogDebug("Published message to exchange {exchange} with headers {@headers}.", exchange, properties.Headers);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Published message to exchange {exchange} has FAILED.", exchange);

                return false;
            }
        }

        private IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];
            string contractId = message.Payload.GetType().GetContractId();
            string tenant = message.GetTenant();

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add(contractId, boundedContext);
            properties.Headers.Add($"{contractId}@{tenant}", boundedContext);

            string ttl = message.GetTTL(); // https://www.rabbitmq.com/ttl.html#per-message-ttl-in-publishers
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;

            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }
    }
}
