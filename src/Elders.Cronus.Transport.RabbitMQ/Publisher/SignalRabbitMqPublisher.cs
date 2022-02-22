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
        private readonly BoundedContextRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly RabbitMqOptions options;

        public SignalRabbitMqPublisher(ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<RabbitMqOptions> optionsMonitor, BoundedContextRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger) : base(tenantResolver, boundedContext.CurrentValue, logger)
        {
            this.rabbitMqNamer = rabbitMqNamer;
            options = optionsMonitor.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                string boundedContext = message.BoundedContext;

                IEnumerable<string> exchanges = rabbitMqNamer.GetExchangeNames(message.Payload.GetType());
                foreach (var exchange in exchanges)
                {
                    var scopedOptions = options.GetOptionsFor(message.BoundedContext);
                    IModel exchangeModel = channelResolver.Resolve(exchange, scopedOptions, boundedContext);
                    IBasicProperties props = exchangeModel.CreateBasicProperties();
                    props = BuildMessageProperties(props, message);

                    byte[] body = serializer.SerializeToBytes(message);
                    exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
                }

                return true;
            }
            catch (Exception)
            {
                // loosed message - ok
                return false;
            }
        }

        private IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add(message.Payload.GetType().GetContractId(), boundedContext);

            string ttl = message.GetTTL(); // https://www.rabbitmq.com/ttl.html#per-message-ttl-in-publishers
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;

            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }
    }
}
