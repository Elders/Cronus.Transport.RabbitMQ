using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.DomainModeling;
using Elders.Cronus.MessageProcessingMiddleware;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy
{
    public class EndpointPerHandler : IEndpointNameConvention
    {
        IPipelineNameConvention pipelineNameConvention;

        public EndpointPerHandler(IPipelineNameConvention pipelineNameConvention)
        {
            this.pipelineNameConvention = pipelineNameConvention;
        }
        public IEnumerable<EndpointDefinition> GetEndpointDefinition(IMessageProcessor messageProcessor)
        {
            var subscriptions = messageProcessor.GetSubscriptions().FirstOrDefault();
            Dictionary<Type, HashSet<Type>> handlers = new Dictionary<Type, HashSet<Type>>();
            var subType = typeof(SubscriberMiddleware);
            foreach (var item in messageProcessor.GetSubscriptions())
            {
                var messageHandlerType = subType.GetField("messageHandlerType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(item) as Type;
                if (!handlers.ContainsKey(messageHandlerType))
                    handlers.Add(messageHandlerType, new HashSet<Type>() { });
                handlers[messageHandlerType].Add(item.MessageType);
            }

            List<string> endpointNames = new List<string>();

            foreach (var item in handlers)
            {
                var pipelineName = pipelineNameConvention.GetPipelineName(item.Value.First());
                var routingHeaders = item.Value
                               .Distinct()
                               .ToDictionary<Type, string, object>(key => key.GetContractId(), val => String.Empty);
                var bc = item.Key.GetBoundedContext().BoundedContextNamespace;
                var handlerType = typeof(IPort).IsAssignableFrom(item.Key) ? "Port" :
                                  typeof(IProjection).IsAssignableFrom(item.Key) ? "Projection" :
                                  typeof(IAggregateRootApplicationService).IsAssignableFrom(item.Key) ? "AppService" :
                                  "Unknown";

                var endpointName = bc + "." + handlerType + "(" + item.Key.Name + ")";
                if (endpointNames.Contains(endpointName))
                    throw new InvalidOperationException("Duplicatie endpoint name " + endpointName);

                endpointNames.Add(endpointName);
                yield return new EndpointDefinition(pipelineName, endpointName, routingHeaders);
            }
        }
    }
}
