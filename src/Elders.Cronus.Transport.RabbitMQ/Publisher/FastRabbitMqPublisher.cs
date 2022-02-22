using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Elders.Cronus.Transport.RabbitMQ.Publisher
{
    public sealed class FastRabbitMqPublisher : Publisher<IFastSignal>
    {
        private readonly ITenantResolver<IMessage> tenantResolver;
        private readonly PublisherChannelResolver channelResolver;
        private readonly FastMessagesRabbitMqNamer rabbitMqNamer;
        private readonly BoundedContext boundedContext;
        private readonly ISerializer serializer;
        private readonly RabbitMqOptions options;

        public FastRabbitMqPublisher(ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<RabbitMqOptions> optionsMonitor, FastMessagesRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer) : base(tenantResolver, boundedContext.CurrentValue)
        {
            this.tenantResolver = tenantResolver;
            this.boundedContext = boundedContext.CurrentValue;
            this.rabbitMqNamer = rabbitMqNamer;
            options = optionsMonitor.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
        }

        public override bool Publish(IFastSignal message, Dictionary<string, string> messageHeaders)
        {
            try
            {
                messageHeaders = (messageHeaders ?? new Dictionary<string, string>());
                if (!messageHeaders.ContainsKey("publish_timestamp"))
                {
                    messageHeaders.Add("publish_timestamp", DateTime.UtcNow.ToFileTimeUtc().ToString());
                }

                if (!messageHeaders.ContainsKey("tenant"))
                {
                    messageHeaders.Add("tenant", tenantResolver.Resolve(message));
                }

                string bc = message.GetType().GetBoundedContext(boundedContext.Name);
                if (messageHeaders.ContainsKey("bounded_context"))
                {
                    messageHeaders["bounded_context"] = bc;
                }
                else
                {
                    string value2 = message.GetType().GetBoundedContext(boundedContext.Name);
                    messageHeaders.Add("bounded_context", bc);
                }

                string empty = string.Empty;
                if (!messageHeaders.ContainsKey("messageid"))
                {
                    DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(13, 3);
                    defaultInterpolatedStringHandler.AppendLiteral("urn:cronus:");
                    defaultInterpolatedStringHandler.AppendFormatted(messageHeaders["bounded_context"]);
                    defaultInterpolatedStringHandler.AppendLiteral(":");
                    defaultInterpolatedStringHandler.AppendFormatted(messageHeaders["tenant"]);
                    defaultInterpolatedStringHandler.AppendLiteral(":");
                    defaultInterpolatedStringHandler.AppendFormatted(Guid.NewGuid());
                    empty = defaultInterpolatedStringHandler.ToStringAndClear();
                    messageHeaders.Add("messageid", empty);
                }
                else
                {
                    empty = messageHeaders["messageid"];
                }

                if (!messageHeaders.ContainsKey("corelationid"))
                {
                    messageHeaders.Add("corelationid", empty);
                }

                messageHeaders.Remove("contract_name");
                messageHeaders.Add("contract_name", message.GetType().GetContractId());
                CronusMessage cronusMessage = new CronusMessage(message, messageHeaders);
                return PublishInternal(cronusMessage);
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                string boundedContext = message.BoundedContext;

                string exchange = rabbitMqNamer.GetExchangeNames(message.Payload.GetType()).SingleOrDefault();
                var scopedOptions = options.GetOptionsFor(message.BoundedContext);
                IModel exchangeModel = channelResolver.Resolve(exchange, scopedOptions, boundedContext);
                IBasicProperties props = exchangeModel.CreateBasicProperties();
                props = BuildMessageProperties(props, message);

                byte[] body = serializer.SerializeToBytes(message);
                exchangeModel.BasicPublish(exchange, string.Empty, false, props, body);

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
            properties.Expiration = "1000";
            properties.Persistent = false;
            properties.DeliveryMode = 1;

            return properties;
        }
    }
}
