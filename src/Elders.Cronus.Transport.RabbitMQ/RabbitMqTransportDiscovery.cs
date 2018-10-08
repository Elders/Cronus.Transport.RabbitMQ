using System.Collections.Generic;
using Elders.Cronus.Discoveries;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqTransportDiscovery : DiscoveryBasedOnExecutingDirAssemblies<ITransport>
    {
        IEnumerable<DiscoveredModel> GetAllModels()
        {
            yield return new DiscoveredModel(typeof(IRabbitMqTransportSettings), typeof(RabbitMqSettings));
            yield return new DiscoveredModel(typeof(ITransport), typeof(RabbitMqTransport));
        }

        protected override DiscoveryResult<ITransport> DiscoverFromAssemblies(DiscoveryContext context)
        {
            var result = new DiscoveryResult<ITransport>();
            result.Models.AddRange(GetAllModels());

            return result;
        }
    }
}
