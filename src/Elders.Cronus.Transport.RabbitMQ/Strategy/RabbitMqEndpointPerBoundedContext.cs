using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.DomainModeling;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy
{
    public class RabbitMqEndpointPerBoundedContext : EndpointNameConvention
    {
        private readonly IPipelineNameConvention pipelineNameConvention;

        public RabbitMqEndpointPerBoundedContext(IPipelineNameConvention pipelineNameConvention)
        {
            this.pipelineNameConvention = pipelineNameConvention;
        }

        private Dictionary<Type, BoundedContextAttribute> MapHandlersToBoundedContext(Type[] handlerTypes)
        {
            return handlerTypes.ToList().ToDictionary(key => key, val => val.GetBoundedContext());
        }

        public override IEnumerable<EndpointDefinition> GetEndpointDefinition(IMessageProcessor messageProcessor)
        {
            var subscriptions = messageProcessor.GetSubscriptions();

            var groupedByName = subscriptions.GroupBy(x => x.Name);
            foreach (var subscriptionGroup in groupedByName)
            {
                var pipeLine = subscriptionGroup.Select(x => pipelineNameConvention.GetPipelineName(x.MessageType)).Distinct();
                if (pipeLine.Count() == 0)
                    throw new ArgumentException("Cannot find pipeline to subscribe to.");
                else if (pipeLine.Count() > 1)
                    throw new ArgumentException("Cannot subscribe to more than one pipeline. Probably you have mixed ICommand and IEvent messages within a single handler.");
                var routingHeaders = subscriptionGroup.Select(x => x.MessageType)
                                .Distinct()
                                .ToDictionary<Type, string, object>(key => key.GetContractId(), val => String.Empty);

                EndpointDefinition endpointDefinition = new EndpointDefinition(pipeLine.Single(), subscriptionGroup.Key, routingHeaders);
                yield return endpointDefinition;
            }
        }
    }
}
