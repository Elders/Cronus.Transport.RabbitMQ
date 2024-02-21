using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ILogger logger;

        public RabbitMqPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, IRabbitMqNamer rabbitMqNamer, IEnumerable<DelegatingPublishHandler> handlers, ILogger logger)
            : base(handlers)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.rabbitMqNamer = rabbitMqNamer;
            this.logger = logger;
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
            props = AttachHeaders(props, message);

            byte[] body = serializer.SerializeToBytes(message);
            exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);
            logger.LogDebug("Published message in exchange {exchange} with headers {@headers}.", exchange, props.Headers);
        }

        protected virtual IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());
            properties.Expiration = message.GetTtl();
            properties.Persistent = true;

            return properties;
        }

        protected virtual IBasicProperties AttachHeaders(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.BoundedContext;

            properties.Headers.Add(message.GetMessageType().GetContractId(), boundedContext);

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
