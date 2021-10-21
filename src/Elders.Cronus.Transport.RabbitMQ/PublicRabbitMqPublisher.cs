using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisher<IPublicEvent>
    {
        public PublicRabbitMqPublisher(ISerializer serializer, IRabbitMqConnectionResolver<PublicRabbitMqOptions> connectionResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, PublicMessagesRabbitMqNamer publicRabbitMqNamer)
            : base(serializer, connectionResolver, tenantResolver, boundedContext, publicRabbitMqNamer)
        {

        }

        protected override IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            if (message.IsRepublished)
            {
                string boundedContext = message.Headers[MessageHeader.BoundedContext];
                string messageContractId = message.Payload.GetType().GetContractId();

                properties.Headers = new Dictionary<string, object>();
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
    }
}
