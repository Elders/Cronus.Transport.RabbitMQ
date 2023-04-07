using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;

        public RabbitMqPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqNamer rabbitMqNamer, ILogger logger)
            : base(tenantResolver, boundedContext.CurrentValue, logger)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            string boundedContext = message.BoundedContext;

            IEnumerable<string> exchanges = GetExistingExchangesNames(message);
            foreach (string exchange in exchanges)
            {
                IEnumerable<IRabbitMqOptions> scopedOptions = GetOptionsFor(message);
                foreach (IRabbitMqOptions scopedOpt in scopedOptions)
                {
                    Publish(message, boundedContext, exchange, scopedOpt);
                }
            }
            return true;
        }

        protected abstract IEnumerable<IRabbitMqOptions> GetOptionsFor(CronusMessage message);

        private void Publish(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions options)
        {
            IModel exchangeModel = channelResolver.Resolve(exchange, options, boundedContext);
            IBasicProperties props = exchangeModel.CreateBasicProperties();
            props = BuildMessageProperties(props, message);

            if (message.PayloadRaw.Length > 0)
            {
                exchangeModel.BasicPublish(exchange, string.Empty, false, props, message.PayloadRaw);
            }
            else
            {
                byte[] body = serializer.SerializeToBytes(message);
                exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
            }
        }

        protected virtual IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add(message.GetMessageType().GetContractId(), boundedContext);
            properties.Expiration = message.GetTtl();
            properties.Persistent = true;

            return properties;
        }

        private IEnumerable<string> GetExistingExchangesNames(CronusMessage message)
        {
            Type messageType = message.GetMessageType();

            IEnumerable<string> exchanges = rabbitMqNamer.GetExchangeNames(messageType);

            if (string.IsNullOrEmpty(message.GetTtl()) == false)
                exchanges = exchanges.Select(e => $"{e}.Delayer");

            return exchanges;
        }
    }
}
