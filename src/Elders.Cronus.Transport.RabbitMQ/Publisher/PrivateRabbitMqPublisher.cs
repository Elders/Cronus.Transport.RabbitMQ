using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PrivateRabbitMqPublisher<TMessage> : RabbitMqPublisherBase<TMessage> where TMessage : IMessage
    {
        public PrivateRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<RabbitMqOptions> optionsMonitor, BoundedContextRabbitMqNamer rabbitMqNamer, ILogger<PrivateRabbitMqPublisher<TMessage>> logger)
            : base(serializer, channelResolver, tenantResolver, boundedContext, optionsMonitor.CurrentValue, rabbitMqNamer, logger)
        {
        }
    }
}
