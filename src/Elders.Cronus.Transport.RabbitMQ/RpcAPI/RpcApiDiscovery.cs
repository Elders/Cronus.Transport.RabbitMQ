using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Elders.Cronus.Hosting;
using Elders.Cronus.Transport.RabbitMQ.RpcAPI;
using Microsoft.Extensions.DependencyInjection;

namespace Elders.Cronus.Transport.RabbitMQ;

public class RpcApiDiscovery : DiscoveryBase<IConsumer<IMessageHandler>>
{
    protected override DiscoveryResult<IConsumer<IMessageHandler>> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<IConsumer<IMessageHandler>>(GetModels(context));
    }

    IEnumerable<DiscoveredModel> GetModels(DiscoveryContext context)
    {
        yield return new DiscoveredModel(typeof(IRpcHost), typeof(RpcHost), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(IRequestResponseFactory), typeof(RequestResponseFactory), ServiceLifetime.Singleton);

        yield return new DiscoveredModel(typeof(IRpc<,>), typeof(RpcEndpoint<,>), ServiceLifetime.Singleton);
    }
}
