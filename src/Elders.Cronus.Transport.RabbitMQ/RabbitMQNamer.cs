using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public static class RabbitMqNamer
    {
        public static string GetExchangeName(Type messageType)
        {
            if (typeof(ICommand).IsAssignableFrom(messageType))
                return GetCommandsPipelineName(messageType);
            else if (typeof(IEvent).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else if (typeof(IScheduledMessage).IsAssignableFrom(messageType))
                return GetEventsPipelineName(messageType);
            else
                throw new Exception(string.Format("The message type '{0}' is not eligible. Please use ICommand or IEvent", messageType));
        }

        static string GetCommandsPipelineName(Type messageType)
        {
            return GetBoundedContext(messageType).ProductNamespace + ".Commands";
        }

        static string GetEventsPipelineName(Type messageType)
        {
            return GetBoundedContext(messageType).ProductNamespace + ".Events";
        }

        public static BoundedContextAttribute GetBoundedContext(Type messageType)
        {
            var boundedContext = messageType.GetBoundedContext();

            if (boundedContext == null)
                throw new Exception(String.Format(@"The assembly '{0}' is missing a BoundedContext attribute in AssemblyInfo.cs! Example: [BoundedContext(""Company.Product.BoundedContext"")]", messageType.Assembly.FullName));
            return boundedContext;
        }
    }
}
