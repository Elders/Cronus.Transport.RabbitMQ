using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        private readonly BoundedContext currentBoundedContext;
        private readonly IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor;
        private readonly IOptionsMonitor<PublicRabbitMqOptions> options;

        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<PublicRabbitMqOptions> options, IRabbitMqNamer rabbitMqNamer, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, tenantResolver, boundedContext, options.CurrentValue, rabbitMqNamer, logger) { }

        protected override IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];
            string messageContractId = message.Payload.GetType().GetContractId();
            string tenant = message.Headers[MessageHeader.Tenant];

            if (message.IsRepublished)
            {
                properties.Headers = new Dictionary<string, object>();

                if (message.GetPublishDelay() > 1000)
                    properties.Headers.Add("x-delay", message.GetPublishDelay());

                foreach (var recipientHandler in message.RecipientHandlers)
                {
                    properties.Headers.Add($"{messageContractId}@{recipientHandler}", boundedContext);
                    properties.Headers.Add($"{messageContractId}@{recipientHandler}@{tenant}", boundedContext);
                }

                return properties;
            }
            else
            {
                properties.Headers.Add($"{messageContractId}@{tenant}", boundedContext);

                return base.BuildMessageProperties(properties, message);
            }
        }
    }
}
