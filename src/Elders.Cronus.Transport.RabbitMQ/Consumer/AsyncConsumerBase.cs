using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.IO;
using System.Threading.Tasks;
using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncConsumerBase<TSubscriber> : AsyncEventingBasicConsumer
    {
        private readonly ILogger logger;
        private readonly ISerializer serializer;
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        private bool isСurrentlyConsuming;

        public AsyncConsumerBase(IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) : base(model)
        {
            this.subscriberCollection = subscriberCollection;
            this.serializer = serializer;
            this.logger = logger;
            isСurrentlyConsuming = false;
            Received += AsyncListener_Received;
        }

        public async Task StopAsync()
        {
            // 1. We detach the listener so ther will be no new messages coming from the queue
            Received -= AsyncListener_Received;

            // 2. Wait to handle any messages in progress
            while (isСurrentlyConsuming)
            {
                // We are trying to wait all consumers to finish their current work.
                // Ofcourse the host could be forcibly shut down but we are doing our best.
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            try
            {
                isСurrentlyConsuming = true;

                if (sender is AsyncEventingBasicConsumer consumer)
                    await DeliverMessageToSubscribers(@event, consumer).ConfigureAwait(false);
            }
            finally
            {
                isСurrentlyConsuming = false;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        protected virtual async Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            try
            {
                cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(ev.Body);
                cronusMessage = ExpandRawPayload(cronusMessage);

                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                foreach (var subscriber in subscribers)
                {
                    subscriber.Process(cronusMessage);
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + cronusMessage is null ? "Failed to deserialize" : MessageAsString(cronusMessage))) { }
            finally
            {
                if (consumer.Model.IsOpen)
                {
                    consumer.Model.BasicAck(ev.DeliveryTag, false);
                }
            }

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

        protected CronusMessage ExpandRawPayload(CronusMessage cronusMessage)
        {
            if (cronusMessage.Payload is null && cronusMessage.PayloadRaw?.Length > 0)
            {
                IMessage payload = (IMessage)serializer.DeserializeFromBytes(cronusMessage.PayloadRaw);
                return new CronusMessage(payload, cronusMessage.Headers);
            }

            return cronusMessage;
        }
    }
}
