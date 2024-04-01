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

        IEnumerable<string> GetExchangeNames(CronusMessage cronusMessage) => GetExchangeNames(cronusMessage.Payload.GetType());
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
                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase) == false)
                {
                    yield return $"{systemMarker}PublicEvents";
                }
                else
                {
                    yield return $"{bc}.{systemMarker}PublicEvents";
                }
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

            // This handles the message types which are not defined in Cronus.
            if (isConventionalMessageType == false)
            {
                yield return $"{bc}.{systemMarker}{messageType.Name}";
            }
        }
    }

    public sealed class PublicMessagesRabbitMqNamer : IRabbitMqNamer
    {
        private readonly BoundedContext boundedContext;

        public PublicMessagesRabbitMqNamer(IOptionsMonitor<BoundedContext> options)
        {
            this.boundedContext = options.CurrentValue;
        }

        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
            {
                string bc = messageType.GetBoundedContext(boundedContext.Name);

                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                yield return $"{systemMarker}PublicEvents";

                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase))
                    yield return $"{bc}.{systemMarker}PublicEvents";
            }
        }
    }

    public sealed class SignalMessagesRabbitMqNamer : IRabbitMqNamer
    {
        private readonly BoundedContext boundedContext;

        public SignalMessagesRabbitMqNamer(IOptionsMonitor<BoundedContext> options)
        {
            this.boundedContext = options.CurrentValue;
        }

        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                yield return $"{systemMarker}Signals";
            }
        }

        public IEnumerable<string> GetExchangeNames(CronusMessage cronusMessage)
        {
            Type payloadType = cronusMessage.Payload.GetType();
            if (typeof(ISignal).IsAssignableFrom(payloadType))
            {
                bool isInternal = boundedContext.Name.Equals(cronusMessage.BoundedContext, StringComparison.OrdinalIgnoreCase);

                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(payloadType) ? "cronus." : string.Empty;

                if (isInternal)
                {
                    yield return $"{boundedContext}.{systemMarker}Signals";
                }
                else
                {
                    yield return $"{systemMarker}Signals";
                }
            }
        }
    }
}

