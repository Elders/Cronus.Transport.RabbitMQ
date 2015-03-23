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

        private void Guard_HandlersMustBelongToSingleBoundedContext(Type[] handlerTypes)
        {
            var boundedContextexts = handlerTypes.Select(x => x.GetBoundedContext()).Distinct();
            if (boundedContextexts.Count() != 1)
            {
                if (boundedContextexts.Count() == 0)
                    throw new ArgumentException("The specified handlers do not belong to any bounded context.");
                else if (boundedContextexts.Count() > 1)
                    throw new ArgumentException("The specified handlers belong to more than one bounded context. Only 1 is allowed.");
            }
        }

        private Dictionary<Type, BoundedContextAttribute> MapHandlersToBoundedContext(Type[] handlerTypes)
        {
            return handlerTypes.ToList().ToDictionary(key => key, val => val.GetBoundedContext());
        }

        public override IEnumerable<EndpointDefinition> GetEndpointDefinition(IMessageProcessor messageProcessor)
        {
            var handlerTypes = messageProcessor.GetSubscriptions().Select(x => x.MessageHandlerType).ToArray();
            Guard_HandlersMustBelongToSingleBoundedContext(handlerTypes);
            var handler = handlerTypes.First();

            var boundedContext = handler.GetBoundedContext();

            string endpointName = String.Format("{0}.{1}", GetBoundedContext(handler).BoundedContextNamespace, messageProcessor.Name);

            var routingHeaders = messageProcessor.GetSubscriptions().Select(x => x.MessageType)
                                .Distinct()
                                .ToDictionary<Type, string, object>(key => key.GetContractId(), val => String.Empty);

            EndpointDefinition endpointDefinition = new EndpointDefinition(pipelineNameConvention.GetPipelineName(handler), endpointName, routingHeaders);
            yield return endpointDefinition;
        }

        private BoundedContextAttribute GetBoundedContext(Type handlerType)
        {
            var boundedContext = handlerType.GetBoundedContext();

            if (boundedContext == null)
                throw new Exception(String.Format(@"The assembly '{0}' is missing a BoundedContext attribute in AssemblyInfo.cs! Example: [BoundedContext(""Company.Product.BoundedContext"")]", handlerType.Assembly.FullName));
            return boundedContext;
        }
    }
}
