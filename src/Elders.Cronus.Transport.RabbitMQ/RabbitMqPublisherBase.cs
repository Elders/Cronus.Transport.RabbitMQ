using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqPublisherBase<>));

        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly IRabbitMqOptions options;

        public RabbitMqPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqOptions options, IRabbitMqNamer rabbitMqNamer)
            : base(tenantResolver, boundedContext.CurrentValue)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.options = options;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                string boundedContext = message.BoundedContext;

                List<string> exchanges = GetExistingExchangesNames(message);
                foreach (string exchange in exchanges)
                {
                    IModel exchangeModel = channelResolver.Resolve(exchange, options, boundedContext);
                    IBasicProperties props = exchangeModel.CreateBasicProperties();
                    props = BuildMessageProperties(props, message);

                    byte[] body = serializer.SerializeToBytes(message);
                    exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.WarnException(ex, () => ex.Message);
                return false;
            }
        }

        protected virtual IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>() { { message.Payload.GetType().GetContractId(), boundedContext } };
            properties.Persistent = true;

            if (message.GetPublishDelay() > 1000)
                properties.Headers.Add("x-delay", message.GetPublishDelay());

            return properties;
        }

        private List<string> GetExistingExchangesNames(CronusMessage message)
        {
            List<string> exchanges = rabbitMqNamer.GetExchangeNames(message.Payload.GetType()).ToList();

            if (message.GetPublishDelay() > 1000)
            {
                exchanges = exchanges.Select(e => $"{e}.Scheduler").ToList();
            }

            return exchanges;
        }
    }
}
