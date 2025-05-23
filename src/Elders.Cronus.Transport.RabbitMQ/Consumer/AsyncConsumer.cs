﻿using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    /// <summary>
    /// Transient secured consumer with some extra connection management.
    /// </summary>
    /// <typeparam name="TSubscriber"></typeparam>
    public class AsyncConsumer<TSubscriber> : AsyncConsumerBase<TSubscriber>
    {
        public AsyncConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger)
            : base(model, subscriberCollection, serializer, logger)
        {
            model.BasicQos(0, 1, false); // prefetch allow to avoid buffer of messages on the flight
            model.BasicConsume(queue, false, string.Empty, this); // we should use autoAck: false to avoid messages loosing

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Consumer for {cronus_subscriber} started.", typeof(TSubscriber).Name);
        }
    }
}
