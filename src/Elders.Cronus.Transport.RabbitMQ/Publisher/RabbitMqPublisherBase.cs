using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
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

            byte[] body = serializer.SerializeToBytes(message);
            exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
        }

        protected virtual IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add(message.Payload.GetType().GetContractId(), boundedContext);

            if (message.GetPublishDelay() > 1000) // ttl for message
                properties.Headers.Add("x-delay", message.GetPublishDelay());

            string ttl = message.GetTTL(); // https://www.rabbitmq.com/ttl.html#per-message-ttl-in-publishers
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;

            properties.Persistent = true;

            return properties;
        }

        private IEnumerable<string> GetExistingExchangesNames(CronusMessage message)
        {
            IEnumerable<string> exchanges = rabbitMqNamer.GetExchangeNames(message.Payload.GetType());

            if (message.GetPublishDelay() > 1000)
            {
                exchanges = exchanges.Select(e => $"{e}.Scheduler");
            }

            return exchanges;
        }
    }
}
