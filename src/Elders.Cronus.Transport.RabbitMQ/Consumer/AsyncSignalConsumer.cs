using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

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

        public AsyncSignalConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) :
            base(model, subscriberCollection, serializer, logger)
        {
            this.subscriberCollection = subscriberCollection;
            model.BasicConsume(queue, false, string.Empty, this);

            logger.Debug(() => $"Consumer for {typeof(TSubscriber).Name} started.");
        }
    }
}
