﻿using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumerDiscovery : DiscoveryBase<IConsumer<IMessageHandler>>
    {
        protected override DiscoveryResult<IConsumer<IMessageHandler>> DiscoverFromAssemblies(DiscoveryContext context)
        {
            return new DiscoveryResult<IConsumer<IMessageHandler>>(GetModels(), services => services.AddOptions<RabbitMqOptions, RabbitMqOptionsProvider>()
                                                                                           .AddOptions<RabbitMqConsumerOptions, RabbitMqConsumerOptionsProvider>());
        }

        IEnumerable<DiscoveredModel> GetModels()
        {
            yield return new DiscoveredModel(typeof(IConnectionFactory), typeof(RabbitMqConnectionFactory<RabbitMqOptions>), ServiceLifetime.Singleton);

            var consumerModel = new DiscoveredModel(typeof(IConsumer<>), typeof(RabbitMqConsumer<>), ServiceLifetime.Singleton);
            consumerModel.CanOverrideDefaults = true;
            yield return consumerModel;

            yield return new DiscoveredModel(typeof(RabbitMqInfrastructure), typeof(RabbitMqInfrastructure), ServiceLifetime.Singleton);
        }
    }
}
