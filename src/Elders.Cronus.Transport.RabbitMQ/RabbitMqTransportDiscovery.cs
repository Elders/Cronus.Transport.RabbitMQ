using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqTransportDiscovery : DiscoveryBasedOnExecutingDirAssemblies<IPublisher<IMessage>>
    {
        protected override DiscoveryResult<IPublisher<IMessage>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IPublisher<IMessage>>(GetModels());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(IRabbitMqSettings), typeof(RabbitMqSettings), ServiceLifetime.Transient);
            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(RabbitMqConnectionFactory), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(IPublisher<>), typeof(RabbitMqPublisher<>));
        }
    }

    public class RabbitMqConsumerDiscovery : DiscoveryBasedOnExecutingDirAssemblies<IConsumer<object>>
    {
        protected override DiscoveryResult<IConsumer<object>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IConsumer<object>>(GetModels());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(IRabbitMqSettings), typeof(RabbitMqSettings), ServiceLifetime.Transient);
            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(RabbitMqConnectionFactory), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(IConsumer<object>), typeof(RabbitMqConsumer<>), ServiceLifetime.Transient);
        }
    }
}
