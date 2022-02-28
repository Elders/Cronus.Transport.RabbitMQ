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
            bool isConventionalMessageType = false;

            if (typeof(ICommand).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}Commands";
                isConventionalMessageType = true;
            }

            if (typeof(IEvent).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}Events";
                isConventionalMessageType = true;
            }

            if (typeof(IScheduledMessage).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}Events";
                isConventionalMessageType = true;
            }

            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}PublicEvents";
                isConventionalMessageType = true;
            }

            if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}Signals";
                isConventionalMessageType = true;
            }

            if (typeof(AggregateCommit).IsAssignableFrom(messageType))
            {
                yield return $"{bc}.{systemMarker}AggregateCommits";
                isConventionalMessageType = true;
            }

            if (isConventionalMessageType == false)
            {
                yield return $"{bc}.{systemMarker}{messageType.Name}";
            }
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

    public sealed class SignalMessagesRabbitMqNamer : IRabbitMqNamer
    {
        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                yield return $"{systemMarker}Signals";
            }
        }
    }
}

