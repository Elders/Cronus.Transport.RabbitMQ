using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqNamer
    {
        /// <summary>
        /// Returns all possible exchange names this message must be published to.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>The exchange names.</returns>
        IEnumerable<string> GetExchangeNames(Type messageType);
    }

    public sealed class BoundedContextRabbitMqNamer : IRabbitMqNamer
    {
        BoundedContext boundedContext;

        public BoundedContextRabbitMqNamer(IOptionsMonitor<BoundedContext> options)
        {
            boundedContext = options.CurrentValue;
        }

        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            string bc = messageType.GetBoundedContext(boundedContext.Name);

            if (typeof(ICommand).IsAssignableFrom(messageType))
                yield return $"{bc}.Commands";

            if (typeof(IEvent).IsAssignableFrom(messageType))
                yield return $"{bc}.Events";

            if (typeof(IScheduledMessage).IsAssignableFrom(messageType))
                yield return $"{bc}.Events";

            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
                yield return $"{bc}.PublicEvents";
        }
    }


    public sealed class PublicMessagesRabbitMqNamer : IRabbitMqNamer
    {
        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
                yield return $"PublicEvents";
        }
    }
}

