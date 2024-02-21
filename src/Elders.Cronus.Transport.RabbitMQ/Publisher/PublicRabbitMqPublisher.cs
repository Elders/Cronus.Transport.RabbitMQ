using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        private readonly IOptionsMonitor<PublicRabbitMqOptionsCollection> options;

        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, IOptionsMonitor<PublicRabbitMqOptionsCollection> options, PublicMessagesRabbitMqNamer publicRabbitMqNamer, IEnumerable<DelegatingPublishHandler> handlers, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, publicRabbitMqNamer, handlers, logger)
        {
            this.options = options;
        }

        protected override IBasicProperties AttachHeaders(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];
            string tenant = message.Headers[MessageHeader.Tenant];
            string contractId = message.GetMessageType().GetContractId();

            if (message.IsRepublished)
            {
                foreach (string recipientHandler in message.RecipientHandlers)
                {
                    properties.Headers.Add($"{contractId}@{recipientHandler}", boundedContext);
                    properties.Headers.Add($"{contractId}@{recipientHandler}@{tenant}", boundedContext);
                }
            }
            else
            {
                properties.Headers.Add($"{contractId}", boundedContext);
                properties.Headers.Add($"{contractId}@{tenant}", boundedContext);
            }

            return properties;
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
