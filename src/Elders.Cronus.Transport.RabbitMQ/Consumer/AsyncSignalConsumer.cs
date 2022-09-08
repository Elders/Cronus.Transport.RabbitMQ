using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    /// <summary>
    /// Transient consumer which consumes messages with autoacknowledging and sends message to handlers without workflow chains.
    /// We use this consumer for non-persistent types of messages.
    /// </summary>
    /// <typeparam name="TSubscriber"></typeparam>
    public class AsyncSignalConsumer<TSubscriber> : AsyncConsumerBase<TSubscriber>
    {
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        public AsyncSignalConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ConsumerFactory<TSubscriber> factory, ILogger logger) :
            base(model, subscriberCollection, serializer, factory, logger)
        {
            this.subscriberCollection = subscriberCollection;
            model.BasicConsume(queue, true, string.Empty, this);

            logger.Debug(() => $"Consumer for {typeof(TSubscriber).Name} started.");
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            try
            {
                cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(ev.Body);
                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                List<Task> deliverTasks = new List<Task>();

                foreach (var subscriber in subscribers)
                {
                    deliverTasks.Add(subscriber.ProcessAsync(cronusMessage));
                }

                await Task.WhenAll(deliverTasks).ConfigureAwait(false);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + cronusMessage is null ? "Failed to deserialize" : MessageAsString(cronusMessage))) { }
        }
    }
}
