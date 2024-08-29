using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Elders.Cronus.Transport.RabbitMQ.Publisher;
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
            yield return new DiscoveredModel(typeof(IRabbitMqNamer), typeof(BoundedContextRabbitMqNamer), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(BoundedContextRabbitMqNamer), typeof(BoundedContextRabbitMqNamer), ServiceLifetime.Singleton);

            yield return new DiscoveredModel(typeof(PrivateRabbitMqPublisher<>), typeof(PrivateRabbitMqPublisher<>), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(PublicRabbitMqPublisher), typeof(PublicRabbitMqPublisher), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(SignalRabbitMqPublisher), typeof(SignalRabbitMqPublisher), ServiceLifetime.Singleton);

            var publisherModel = new DiscoveredModel(typeof(IPublisher<>), typeof(PrivateRabbitMqPublisher<>), ServiceLifetime.Singleton);
            publisherModel.CanOverrideDefaults = true;
            yield return publisherModel;

            var publicPublisherModel = new DiscoveredModel(typeof(IPublisher<IPublicEvent>), typeof(PublicRabbitMqPublisher), ServiceLifetime.Singleton);
            publicPublisherModel.CanOverrideDefaults = true;
            yield return publicPublisherModel;

            var signalPublisherModel = new DiscoveredModel(typeof(IPublisher<ISignal>), typeof(SignalRabbitMqPublisher), ServiceLifetime.Singleton);
            signalPublisherModel.CanOverrideDefaults = true;
            yield return signalPublisherModel;

            yield return new DiscoveredModel(typeof(RabbitMqInfrastructure), typeof(RabbitMqInfrastructure), ServiceLifetime.Singleton);

            yield return new DiscoveredModel(typeof(ConnectionResolver), typeof(ConnectionResolver), ServiceLifetime.Singleton);
            yield return new DiscoveredModel(typeof(PublisherChannelResolver), typeof(PublisherChannelResolver), ServiceLifetime.Singleton);
        }
    }
}
