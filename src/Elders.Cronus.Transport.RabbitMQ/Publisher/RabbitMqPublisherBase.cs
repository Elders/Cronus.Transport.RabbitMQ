using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ILogger logger;
        private readonly IRabbitMqOptions options;

        public RabbitMqPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqOptions options, IRabbitMqNamer rabbitMqNamer, ILogger logger)
            : base(tenantResolver, boundedContext.CurrentValue, logger)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.options = options;
            this.rabbitMqNamer = rabbitMqNamer;
            this.logger = logger;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                string boundedContext = message.BoundedContext;

                IEnumerable<string> exchanges = GetExistingExchangesNames(message);
                foreach (string exchange in exchanges)
                {
                    IRabbitMqOptions scopedOptions = options.GetOptionsFor(boundedContext);
                    IModel exchangeModel = channelResolver.Resolve(exchange, scopedOptions, boundedContext);
                    IBasicProperties props = exchangeModel.CreateBasicProperties();
                    props = BuildMessageProperties(props, message);

                    byte[] body = serializer.SerializeToBytes(message);
                    exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex, () => $"Failed to publish message.{Environment.NewLine}{AsString(message, serializer)}");
            }

            return false;

            static string AsString(CronusMessage message, ISerializer serializer)
            {
                using (var stream = new MemoryStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    serializer.Serialize(stream, message);
                    stream.Position = 0;
                    return reader.ReadToEnd();
                }
            }
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
