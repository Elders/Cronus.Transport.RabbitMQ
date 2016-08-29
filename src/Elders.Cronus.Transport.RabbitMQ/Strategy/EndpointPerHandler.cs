using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.DomainModeling;
using Elders.Cronus.MessageProcessing;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy
{
    public class EndpointPerHandler : IEndpointNameConvention
    {
        IPipelineNameConvention pipelineNameConvention;

        public EndpointPerHandler(IPipelineNameConvention pipelineNameConvention)
        {
            this.pipelineNameConvention = pipelineNameConvention;
        }

        public IEnumerable<EndpointDefinition> GetEndpointDefinition(SubscriptionMiddleware subscriptionMiddleware)
        {
            Dictionary<string, HashSet<Type>> handlers = new Dictionary<string, HashSet<Type>>();
            var subType = typeof(SubscriptionMiddleware);
            foreach (var item in subscriptionMiddleware.Subscribers)
            {
                var messageHandlerType = subType.GetField("messageHandlerType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(item) as Type;
                if (!handlers.ContainsKey(item.Id))
                    handlers.Add(item.Id, new HashSet<Type>() { });
                foreach (var messageType in item.MessageTypes)
                {
                    handlers[item.Id].Add(messageType);
                }
            }

            List<string> endpointNames = new List<string>();

            foreach (var item in handlers)
            {
                var pipelineName = pipelineNameConvention.GetPipelineName(item.Value.First());
                var routingHeaders = item.Value
                               .Distinct()
                               .ToDictionary<Type, string, object>(key => key.GetContractId(), val => String.Empty);
                var bc = item.Value.First().GetBoundedContext().BoundedContextNamespace;

                var endpointName = bc + "." + "(" + item.Key + ")";
                if (endpointNames.Contains(endpointName))
                    throw new InvalidOperationException("Duplicatie endpoint name " + endpointName);

                endpointNames.Add(endpointName);
                yield return new EndpointDefinition(pipelineName, endpointName, routingHeaders);
            }
        }
    }
}
