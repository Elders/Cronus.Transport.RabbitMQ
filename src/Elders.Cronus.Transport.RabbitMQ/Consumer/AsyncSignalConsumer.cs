using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
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
        private readonly ISerializer serializer;
        private readonly ILogger logger;
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        public AsyncSignalConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) :
            base(model, subscriberCollection, serializer, logger)
        {
            this.serializer = serializer;
            this.logger = logger;
            this.subscriberCollection = subscriberCollection;
            model.BasicConsume(queue, true, string.Empty, this);
        }

        protected override async Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            try
            {
                cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(ev.Body);
                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                foreach (var subscriber in subscribers)
                {
                    subscriber.Process(cronusMessage);
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + cronusMessage is null ? "Failed to deserialize" : MessageAsString(cronusMessage))) { }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private string MessageAsString(CronusMessage message)
        {
            using (var stream = new MemoryStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                serializer.Serialize(stream, message);
                stream.Position = 0;
                return reader.ReadToEnd();
            }
        }
    }
}
