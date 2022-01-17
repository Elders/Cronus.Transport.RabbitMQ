using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Microsoft.Extensions.DependencyInjection;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqPublisherDiscovery : DiscoveryBase<IPublisher<IMessage>>
    {
        protected override DiscoveryResult<IPublisher<IMessage>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IPublisher<IMessage>>(GetModels(), services => services
                                                                                        .AddOptions<RabbitMqOptions, RabbitMqOptionsProvider>()
                                                                                        .AddOptions<PublicRabbitMqOptions, PublicRabbitMqOptionsProvider>());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(IRabbitMqConnectionResolver<>), typeof(RabbitMqConnectionResolver<>), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(RabbitMqConnectionResolver<>), typeof(RabbitMqConnectionResolver<>), ServiceLifetime.Singleton);


            yield return new DiscoveredModel(typeof(BoundedContextRabbitMqNamer), typeof(BoundedContextRabbitMqNamer), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(PublicMessagesRabbitMqNamer), typeof(PublicMessagesRabbitMqNamer), ServiceLifetime.Singleton);

            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(ConnectionFactory), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(ConnectionFactory), typeof(ConnectionFactory), ServiceLifetime.Singleton);

            yield return new DiscoveredModel(typeof(RabbitMqConnectionFactory<>), typeof(RabbitMqConnectionFactory<>), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(PrivateRabbitMqPublisher<>), typeof(PrivateRabbitMqPublisher<>), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(PublicRabbitMqPublisher), typeof(PublicRabbitMqPublisher), ServiceLifetime.Singleton);

            var publisherModel = new DiscoveredModel(typeof(IPublisher<>), typeof(PrivateRabbitMqPublisher<>), ServiceLifetime.Singleton);
            publisherModel.CanOverrideDefaults = true;
            yield return publisherModel;

            var publicPublisherModel = new DiscoveredModel(typeof(IPublisher<IPublicEvent>), typeof(PublicRabbitMqPublisher), ServiceLifetime.Singleton);
            publicPublisherModel.CanOverrideDefaults = true;
            yield return publicPublisherModel;

            yield return new DiscoveredModel(typeof(RabbitMqInfrastructure), typeof(RabbitMqInfrastructure), ServiceLifetime.Singleton);
        }
    }
}
