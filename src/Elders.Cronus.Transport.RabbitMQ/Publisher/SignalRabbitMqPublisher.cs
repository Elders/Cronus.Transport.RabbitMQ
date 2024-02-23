using System;
using System.Collections.Generic;
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
        private readonly SignalMessagesRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly ILogger<SignalRabbitMqPublisher> logger;
        private readonly PublicRabbitMqOptionsCollection options;

        public SignalRabbitMqPublisher(IOptionsMonitor<BoundedContext> boundedContextOptionsMonitor, IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor, IOptionsMonitor<PublicRabbitMqOptionsCollection> optionsMonitor, SignalMessagesRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger, IEnumerable<DelegatingPublishHandler> handlers)
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

        protected override bool PublishInternal(CronusMessage message)
        {
            Type messageType = null;

            try
            {
                string messageBC = message.BoundedContext;
                messageType = message.GetMessageType();
                IEnumerable<string> exchanges = rabbitMqNamer.GetExchangeNames(message);
                bool isInternalSignal = boundedContext.Name.Equals(messageBC, StringComparison.OrdinalIgnoreCase); // if the message will be published internally to the same BC

                if (isInternalSignal)
                {
                    foreach (var exchange in exchanges)
                    {
                        PublishInternally(message, messageBC, exchange, internalOptions);
                    }
                }
                else
                {
                    foreach (var exchange in exchanges)
                    {
                        PublishPublically(message, messageBC, exchange, options.PublicClustersOptions);
                    }
                }

                return true;
            }
            catch (Exception ex) when (logger.WarnException(ex, () => $"Unable to publish {messageType}"))
            {
                return false;
            }
        }

        private void PublishInternally(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions internalOptions)
        {
            IModel exchangeModel = channelResolver.Resolve(exchange, internalOptions, boundedContext);
            IBasicProperties props = exchangeModel.CreateBasicProperties();
            props = BuildMessageProperties(props, message);

            PublishUsingChannel(message, exchange, exchangeModel, props);
        }

        private void PublishPublically(CronusMessage message, string boundedContext, string exchange, IEnumerable<IRabbitMqOptions> scopedOptions)
        {
            IBasicProperties props = null;

            foreach (var opt in scopedOptions)
            {
                IModel exchangeModel = channelResolver.Resolve(exchange, opt, boundedContext);

                if (props == null)
                {
                    props = exchangeModel.CreateBasicProperties();
                    props = BuildMessageProperties(props, message);
                }

                PublishUsingChannel(message, exchange, exchangeModel, props);
            }
        }

        private void PublishUsingChannel(CronusMessage message, string exchange, IModel exchangeModel, IBasicProperties properties)
        {
            byte[] body = serializer.SerializeToBytes(message);
            exchangeModel.BasicPublish(exchange, string.Empty, false, properties, body);
        }

        private IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string contractId = message.GetMessageType().GetContractId();
            string boundedContext = message.BoundedContext;
            string tenant = message.GetTenant();

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add($"{contractId}", boundedContext);
            properties.Headers.Add($"{contractId}@{tenant}", boundedContext);
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());
            properties.Expiration = message.GetTtl();
            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }
    }
}
