using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class AsyncConsumerBase : AsyncEventingBasicConsumer
    {
        protected readonly ILogger logger;
        protected readonly ISerializer serializer;
        protected readonly IModel model;
        private bool isСurrentlyConsuming;

        public AsyncConsumerBase(IModel model, ISerializer serializer, ILogger logger) : base(model)
        {
            this.model = model;
            this.serializer = serializer;
            this.logger = logger;
            isСurrentlyConsuming = false;
            Received += AsyncListener_Received;
        }

        protected abstract Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer);

        public async Task StopAsync()
        {
            // 1. We detach the listener so ther will be no new messages coming from the queue
            Received -= AsyncListener_Received;

            // 2. Wait to handle any messages in progress
            while (isСurrentlyConsuming)
            {
                // We are trying to wait all consumers to finish their current work.
                // Ofcourse the host could be forcibly shut down but we are doing our best.

                await Task.Delay(10).ConfigureAwait(false);
            }

            if (model.IsOpen)
                model.Abort();
        }

        private async Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            try
            {
                isСurrentlyConsuming = true;

                if (sender is AsyncEventingBasicConsumer consumer)
                    await DeliverMessageToSubscribersAsync(@event, consumer).ConfigureAwait(false);
            }
            finally
            {
                isСurrentlyConsuming = false;
            }
        }
    }

    public class AsyncConsumerBase<TSubscriber> : AsyncConsumerBase
    {
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        public AsyncConsumerBase(IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) : base(model, serializer, logger)
        {
            this.subscriberCollection = subscriberCollection;
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            try
            {
                cronusMessage = serializer.DeserializeFromBytes<CronusMessage>(ev.Body.ToArray());
                cronusMessage = ExpandRawPayload(cronusMessage);

                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                List<Task> deliverTasks = new List<Task>();

                foreach (var subscriber in subscribers)
                {
                    deliverTasks.Add(subscriber.ProcessAsync(cronusMessage));
                }

                await Task.WhenAll(deliverTasks).ConfigureAwait(false);

                // Try find some errors
                StringBuilder subscriberErrors = new StringBuilder();
                bool hasErrors = false;
                foreach (Task subscriberCompletedTasks in deliverTasks)
                {
                    if (subscriberCompletedTasks.IsFaulted)
                    {
                        hasErrors = true;
                        subscriberErrors.AppendLine(subscriberCompletedTasks.Exception.ToString());
                    }
                }
                if (hasErrors)
                {
                    logger.LogError(subscriberErrors.ToString());
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + cronusMessage is null ? "Failed to deserialize" : serializer.SerializeToString(cronusMessage))) { }
            finally
            {
                if (consumer.Model.IsOpen)
                {
                    consumer.Model.BasicAck(ev.DeliveryTag, false);
                }
            }
        }

        protected CronusMessage ExpandRawPayload(CronusMessage cronusMessage)
        {
            if (cronusMessage.Payload is null && cronusMessage.PayloadRaw?.Length > 0)
            {
                IMessage payload = serializer.DeserializeFromBytes<IMessage>(cronusMessage.PayloadRaw);
                return new CronusMessage(payload, cronusMessage.Headers);
            }

            return cronusMessage;
        }
    }
}
