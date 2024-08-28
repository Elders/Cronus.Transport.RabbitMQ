using Elders.Cronus.EventStore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqNamer
    {
        IEnumerable<string> Get_PublishTo_ExchangeNames(Type messageType);

        IEnumerable<string> Get_BindTo_ExchangeNames(Type messageType);

        string Get_QueueName(Type messageType, bool useFanoutMode = false);

        IEnumerable<string> Get_FederationUpstream_ExchangeNames(Type messageType);

        IEnumerable<string> GetExchangeNames(CronusMessage cronusMessage) => Get_PublishTo_ExchangeNames(cronusMessage.Payload.GetType());
    }

    public sealed class BoundedContextRabbitMqNamer : IRabbitMqNamer
    {
        BoundedContext boundedContext;

        public BoundedContextRabbitMqNamer(IOptionsMonitor<BoundedContext> options)
        {
            boundedContext = options.CurrentValue;
        }

        public IEnumerable<string> Get_BindTo_ExchangeNames(Type messageType)
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

                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase))
                    yield return $"{bc}.{systemMarker}Events";
                else
                    yield return $"{systemMarker}PublicEvents";

                isConventionalMessageType = true;
            }

            if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                yield return $"{systemMarker}Signals";
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

        public IEnumerable<string> Get_FederationUpstream_ExchangeNames(Type messageType)
        {
            if (typeof(IPublicEvent).IsAssignableFrom(messageType))
            {
                yield return "PublicEvents";
            }
            else if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                yield return $"Signals";
            }
            else
                yield break;
        }

        public IEnumerable<string> Get_PublishTo_ExchangeNames(Type messageType)
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
                yield return $"{systemMarker}PublicEvents";

                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase))
                    yield return $"{bc}.{systemMarker}Events";

                isConventionalMessageType = true;
            }

            if (typeof(ISignal).IsAssignableFrom(messageType))
            {
                if (boundedContext.Name.Equals(bc, StringComparison.OrdinalIgnoreCase))
                    yield return $"{bc}.{systemMarker}Signals";
                else
                    yield return $"{systemMarker}Signals";

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

        public string Get_QueueName(Type messageType, bool useFanoutMode = false)
        {
            if (useFanoutMode)
            {
                return $"{boundedContext}.{messageType.Name}.{Environment.MachineName}";
            }
            else
            {
                string systemMarker = typeof(ISystemHandler).IsAssignableFrom(messageType) ? "cronus." : string.Empty;
                // This is the default
                return $"{boundedContext}.{systemMarker}{messageType.Name}";
            }
        }
    }
}

