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
        private readonly SignalMessagesRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly ILogger<SignalRabbitMqPublisher> logger;
        private readonly PublicRabbitMqOptionsCollection options;

        public SignalRabbitMqPublisher(ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<PublicRabbitMqOptionsCollection> optionsMonitor, SignalMessagesRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger)
            : base(tenantResolver, boundedContext.CurrentValue, logger)
        {
            this.rabbitMqNamer = rabbitMqNamer;
            options = optionsMonitor.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.logger = logger;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            Type messageType = null;

            try
            {
                string boundedContext = message.BoundedContext;

                messageType = message.GetMessageType();

                IEnumerable<string> exchanges = rabbitMqNamer.GetExchangeNames(messageType);
                foreach (var exchange in exchanges)
                {
                    Publish(message, boundedContext, exchange, options.PublicClustersOptions);
                }

                return true;
            }
            catch (Exception ex) when (logger.WarnException(ex, () => $"Unable to publish {messageType}"))
            {
                return false;
            }
        }

        private void Publish(CronusMessage message, string boundedContext, string exchange, IEnumerable<IRabbitMqOptions> scopedOptions)
        {
            foreach (var opt in scopedOptions)
            {
                IModel exchangeModel = channelResolver.Resolve(exchange, opt, boundedContext);
                IBasicProperties props = exchangeModel.CreateBasicProperties();
                props = BuildMessageProperties(props, message);

                byte[] body = serializer.SerializeToBytes(message);
                exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
            }
        }

        private IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add(message.GetMessageType().GetContractId(), boundedContext);
            properties.Expiration = message.GetTtl();
            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }
    }
}
