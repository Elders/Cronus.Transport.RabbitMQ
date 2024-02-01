using System.Collections.Generic;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        private readonly IOptionsMonitor<PublicRabbitMqOptionsCollection> options;

        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, IOptionsMonitor<PublicRabbitMqOptionsCollection> options, PublicMessagesRabbitMqNamer publicRabbitMqNamer, IEnumerable<DelegatingPublishHandler> handlers)
            : base(serializer, channelResolver, publicRabbitMqNamer, handlers)
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
                properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());
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
