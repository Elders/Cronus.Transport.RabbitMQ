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

    public abstract class PublicMessagesRabbitMqNamerBase : IRabbitMqNamer
    {
        private readonly BoundedContext boundedContext;

        public PublicMessagesRabbitMqNamerBase(IOptionsMonitor<BoundedContext> options)
        {
            this.boundedContext = options.CurrentValue;
        }

        public IEnumerable<string> GetExchangeNames(Type messageType)
        {
            if (GetTypeIdentifier().IsAssignableFrom(messageType))
            {
                if (IsInfrastructureBootstrapMessageType(messageType))
                {
                    yield return GetNameIdentifier();
                    yield break;
                }

                string bc = messageType.GetBoundedContext(boundedContext.Name);

                // No BoundedContext here, because the bounded context is global here
                string systemMarker = typeof(ISystemMessage).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                yield return $"{systemMarker}{GetNameIdentifier()}";

                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase))
                    yield return $"{bc}.{systemMarker}{GetNameIdentifier()}";
            }
        }

        /// <summary>
        /// When starting the system we do a boostrap procedure where the exchanges are being created based on base types and interfaces.
        /// These are not concrete messages which can be sent via the network.
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        private bool IsInfrastructureBootstrapMessageType(Type messageType)
        {
            return messageType.IsInterface || messageType.IsAbstract;
        }

        protected abstract string GetNameIdentifier();
        protected abstract Type GetTypeIdentifier();
    }

    public sealed class PublicMessagesRabbitMqNamer : PublicMessagesRabbitMqNamerBase
    {
        private const string PublicEventNameIdentifier = "PublicEvents";
        private static Type PublicEventTypeIdentifier = typeof(IPublicEvent);


        public PublicMessagesRabbitMqNamer(IOptionsMonitor<BoundedContext> options) : base(options) { }

        protected override string GetNameIdentifier() => PublicEventNameIdentifier;

        protected override Type GetTypeIdentifier() => PublicEventTypeIdentifier;

    }

    public sealed class SignalMessagesRabbitMqNamer : PublicMessagesRabbitMqNamerBase
    {
        private const string SignalNameIdentifier = "Signals";
        private static Type SignalTypeIdentifier = typeof(ISignal);

        public SignalMessagesRabbitMqNamer(IOptionsMonitor<BoundedContext> options) : base(options) { }

        protected override string GetNameIdentifier() => SignalNameIdentifier;

        protected override Type GetTypeIdentifier() => SignalTypeIdentifier;
    }
}

