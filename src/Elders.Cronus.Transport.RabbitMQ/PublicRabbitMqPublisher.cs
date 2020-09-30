using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisher<IPublicEvent>
    {
        public PublicRabbitMqPublisher(ISerializer serializer, RabbitMqConnectionResolver connectionResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, PublicMessagesRabbitMqNamer publicRabbitMqNamer)
            : base(serializer, connectionResolver, tenantResolver, boundedContext, publicRabbitMqNamer)
        {

        }
    }
}
