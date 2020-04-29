using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PrivateRabbitMqPublisher<TMessage> : RabbitMqPublisher<TMessage>
        where TMessage : IMessage
    {
        public PrivateRabbitMqPublisher(ISerializer serializer, RabbitMqConnectionFactory<RabbitMqOptions> connectionFactory, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, BoundedContextRabbitMqNamer bcRabbitMqNamer)
            : base(serializer, connectionFactory, tenantResolver, boundedContext, bcRabbitMqNamer)
        {
        }
    }
}
