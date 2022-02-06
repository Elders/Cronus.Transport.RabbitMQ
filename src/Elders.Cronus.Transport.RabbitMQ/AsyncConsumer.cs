﻿using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncConsumer<TSubscriber> : AsyncEventingBasicConsumer
    {
        private readonly ILogger logger;
        private readonly ISerializer serializer;
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        private bool isСurrentlyConsuming;

        public AsyncConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) : base(model)
        {
            this.logger = logger;
            this.subscriberCollection = subscriberCollection;
            this.serializer = serializer;

            Received += AsyncListener_Received;
            model.BasicQos(0, 1, false); // prefetch allow to avoid buffer of messages on the flight
            model.BasicConsume(queue, false, string.Empty, this); // we should use autoAck: false to avoid messages loosing
            isСurrentlyConsuming = false;
        }

        public async Task StopAsync()
        {
            Received -= AsyncListener_Received;

            while (isСurrentlyConsuming)
            {
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

        private async Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
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
    }
}

