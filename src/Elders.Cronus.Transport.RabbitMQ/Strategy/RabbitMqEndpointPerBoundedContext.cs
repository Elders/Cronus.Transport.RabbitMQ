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

        public override IEnumerable<EndpointDefinition> GetEndpointDefinition(Type[] handlerTypes)
        {
            if (handlerTypes.Select(x => x.GetBoundedContext()).Distinct().Count() != 1)
                throw new ArgumentException("Cannot find bounded context or more than one bounded context are in use.");
            var handler = handlerTypes.First();
            var boundedContext = handler.GetBoundedContext();

            string endpointName = String.Empty;
            if (typeof(IAggregateRootApplicationService).IsAssignableFrom(handler))
                endpointName = GetAppServiceEndpointName(handler);
            else if (typeof(IPort).IsAssignableFrom(handler))
                endpointName = GetPortEndpointName(handler);
            else if (typeof(IProjection).IsAssignableFrom(handler))
                endpointName = GetProjectionEndpointName(handler);

            var routingHeaders = (from handlerType in handlerTypes
                                  from handlerMethod in handlerType.GetMethods()
                                  from handlerMethodParameter in handlerMethod.GetParameters()
                                  where handlerMethod.Name == "Handle"
                                  select handlerMethodParameter.ParameterType)
                                  .Distinct()
                                 .ToDictionary<Type, string, object>(key => key.GetContractId(), val => String.Empty);

            EndpointDefinition endpointDefinition = new EndpointDefinition(pipelineNameConvention.GetPipelineName(handler), endpointName, routingHeaders);
            yield return endpointDefinition;
        }

        protected override string GetAppServiceEndpointName(Type handlerType)
        {
            return String.Format("{0}.Commands", GetBoundedContext(handlerType).BoundedContextNamespace);
        }

        protected override string GetPortEndpointName(Type handlerType)
        {
            return String.Format("{0}.Ports", GetBoundedContext(handlerType).BoundedContextNamespace);
        }

        protected override string GetProjectionEndpointName(Type handlerType)
        {
            return String.Format("{0}.Projections", GetBoundedContext(handlerType).BoundedContextNamespace);
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
