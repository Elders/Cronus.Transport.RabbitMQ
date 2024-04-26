using System.Collections.Generic;
using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        private readonly BoundedContext currentBoundedContext;
        private readonly IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor;
        private readonly IOptionsMonitor<PublicRabbitMqOptionsCollection> options;

        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, IOptionsMonitor<BoundedContext> boundedContextOptionsMonitor, IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor, IOptionsMonitor<PublicRabbitMqOptionsCollection> options, IRabbitMqNamer rabbitMqNamer, IEnumerable<DelegatingPublishHandler> handlers, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, rabbitMqNamer, handlers, logger)
        {
            this.currentBoundedContext = boundedContextOptionsMonitor.CurrentValue;
            this.internalOptionsMonitor = internalOptionsMonitor;
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
                    properties.Headers.Add($"{contractId}@{recipientHandler}", boundedContext); // Remove in v11
                    properties.Headers.Add($"{contractId}@{recipientHandler}@{tenant}", boundedContext);
                }
            }
            else
            {
                properties.Headers.Add($"{contractId}", boundedContext);// Remove in v11
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

            if (currentBoundedContext.Name.Equals(message.BoundedContext, System.StringComparison.OrdinalIgnoreCase))
            {
                IRabbitMqOptions internalRmqOptions = internalOptionsMonitor.CurrentValue.GetOptionsFor(currentBoundedContext.Name);

                yield return internalRmqOptions;
            }
        }
    }
}
