using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumerDiscovery : DiscoveryBase<IConsumer<object>>
    {
        protected override DiscoveryResult<IConsumer<object>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IConsumer<object>>(GetModels());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(RabbitMqSettings), typeof(RabbitMqSettings), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(RabbitMqConnectionFactory), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(IConsumer<>), typeof(RabbitMqConsumer<>), ServiceLifetime.Singleton);
        }
    }
}
