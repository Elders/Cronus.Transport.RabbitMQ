using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<PublicRabbitMqOptions> options, IRabbitMqNamer rmqNamer, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, tenantResolver, boundedContext, options.CurrentValue, rmqNamer, logger) { }

        protected override IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];
            string messageContractId = message.Payload.GetType().GetContractId();
            string tenant = message.Headers[MessageHeader.Tenant];

            properties.Headers = new Dictionary<string, object>();

            if (message.IsRepublished)
            {
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
                properties.Headers.Add($"{messageContractId}", boundedContext);
                properties.Headers.Add($"{messageContractId}@{tenant}", boundedContext);

                return properties;
            }
        }
    }
}
