using Elders.Cronus.EventStore;
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
            string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;

            string bc = messageType.GetBoundedContext(boundedContext.Name);

            if (typeof(ICommand).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}Commands";

            else if (typeof(IEvent).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}Events";

            else if (typeof(IScheduledMessage).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}Events";

            else if (typeof(IPublicEvent).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}PublicEvents";

            else if (typeof(ISignal).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}Signals";

            else if (typeof(AggregateCommit).IsAssignableFrom(messageType))
                yield return $"{bc}.{systemMarker}AggregateCommits";

            else
                yield return $"{bc}.{systemMarker}{messageType.Name}";
        }
    }


    public sealed class PublicMessagesRabbitMqNamer : IRabbitMqNamer
    {
        public IEnumerable<string> GetExchangeNames(Type messageType)
        {

            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
            {
                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                yield return $"{systemMarker}PublicEvents";
            }
        }
    }
}

