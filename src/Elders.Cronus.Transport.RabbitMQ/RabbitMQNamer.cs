using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public static class RabbitMqNamer
    {
        public static string GetExchangeName(string boundedContext, Type messageType)
        {
            if (typeof(ICommand).IsAssignableFrom(messageType))
                return $"{boundedContext}.Commands";
            else if (typeof(IEvent).IsAssignableFrom(messageType))
                return $"{boundedContext}.Events";
            else if (typeof(IScheduledMessage).IsAssignableFrom(messageType))
                return $"{boundedContext}.Events";
            else
                return $"{boundedContext}.Custom";
        }
    }
}
