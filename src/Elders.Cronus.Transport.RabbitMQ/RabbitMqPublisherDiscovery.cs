using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqPublisherDiscovery : DiscoveryBasedOnExecutingDirAssemblies<IPublisher<IMessage>>
    {
        protected override DiscoveryResult<IPublisher<IMessage>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IPublisher<IMessage>>(GetModels());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(RabbitMqSettings), typeof(RabbitMqSettings), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(RabbitMqConnectionFactory), ServiceLifetime.Singleton);

            var publisherModel = new DiscoveredModel(typeof(IPublisher<>), typeof(RabbitMqPublisher<>), ServiceLifetime.Singleton);
            publisherModel.CanOverrideDefaults = true;
            yield return publisherModel;
        }
    }
}
