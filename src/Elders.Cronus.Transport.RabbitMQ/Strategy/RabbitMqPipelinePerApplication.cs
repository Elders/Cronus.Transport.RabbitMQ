using System;
using Elders.Cronus.DomainModeling;
using Elders.Cronus.Pipeline;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy
{
    public class RabbitMqPipelinePerApplication : PipelineNameConvention
    {
        public override string GetPipelineName(Type messageType)
        {
            if (typeof(ICommand).IsAssignableFrom(messageType))
                return GetCommandsPipelineName(messageType);
            else if (typeof(IEvent).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else if (typeof(IAggregateRootApplicationService).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else if (typeof(IPort).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else if (typeof(IProjection).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else return String.Empty;
        }

        protected override string GetCommandsPipelineName(Type messageType)
        {
            return GetBoundedContext(messageType).ProductNamespace + ".Commands";
        }

        protected override string GetEventsPipelineName(Type messageType)
        {
            return GetBoundedContext(messageType).ProductNamespace + ".Events";
        }

        private BoundedContextAttribute GetBoundedContext(Type messageType)
        {
            var boundedContext = messageType.GetBoundedContext();

            if (boundedContext == null)
                throw new Exception(String.Format(@"The assembly '{0}' is missing a BoundedContext attribute in AssemblyInfo.cs! Example: [BoundedContext(""Company.Product.BoundedContext"")]", messageType.Assembly.FullName));
            return boundedContext;
        }
    }
}
