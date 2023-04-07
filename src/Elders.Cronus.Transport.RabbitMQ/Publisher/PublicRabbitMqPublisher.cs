using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        private readonly IOptionsMonitor<PublicRabbitMqOptionsCollection> options;

        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<PublicRabbitMqOptionsCollection> options, PublicMessagesRabbitMqNamer publicRabbitMqNamer, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, tenantResolver, boundedContext, publicRabbitMqNamer, logger)
        {
            this.options = options;
        }

        protected override IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            if (message.IsRepublished)
            {
                string boundedContext = message.Headers[MessageHeader.BoundedContext];
                string messageContractId = message.GetMessageType().GetContractId();

                properties.Headers = new Dictionary<string, object>();
                properties.Expiration = message.GetTtl();

                foreach (var recipientHandler in message.RecipientHandlers)
                {
                    properties.Headers.Add($"{messageContractId}@{recipientHandler}", boundedContext);
                }

                return properties;
            }
            else
            {
                return base.BuildMessageProperties(properties, message);
            }
        }

        protected override IEnumerable<IRabbitMqOptions> GetOptionsFor(CronusMessage message)
        {
            foreach (var publicRabbitMqConfig in options.CurrentValue.PublicClustersOptions)
            {
                yield return publicRabbitMqConfig;
            }
        }
    }
}
