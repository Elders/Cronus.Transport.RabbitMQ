﻿using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncRabbitMqContinuousConsumerFactory<T>
    {
        private readonly ILogger logger = CronusLogger.CreateLogger(typeof(AsyncRabbitMqContinuousConsumerFactory<>));
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private IConnection connection;

        public AsyncRabbitMqContinuousConsumerFactory(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, IConnectionFactory connectionFactory)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            connection = connectionFactory.CreateConnection();
        }

        public void CreateConsumers()
        {
            string queue = GetQueueName(boundedContext.Name);

            if (connection.IsOpen)
            {
                for (int i = 0; i < consumerOptions.WorkersCount; i++)
                {
                    var subscriber = connection.CreateModel();
                    var asyncListener = new AsyncEventingBasicConsumer(subscriber);
                    asyncListener.Received += AsyncListener_Received;
                    subscriber.BasicQos(0, 1, false); // prefetch allow to avoid buffer of messages on the flight
                    subscriber.BasicConsume(queue, false, string.Empty, asyncListener); // we should use autoAck: false to avoid messages loosing
                }
            }
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            if (sender is AsyncEventingBasicConsumer consumer)
            {
                return DeliverMessageToSubscribers(@event, consumer);
            }

            logger.Error(() => $"There is no consumer for {Convert.ToBase64String(@event.Body.ToArray())}");
            return Task.CompletedTask;
        }

        private Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            var cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(ev.Body.ToArray());
            try
            {
                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                foreach (var subscriber in subscribers)
                {
                    subscriber.Process(cronusMessage);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + JsonSerializer.Serialize(cronusMessage));
            }
            finally
            {
                consumer.Model.BasicAck(ev.DeliveryTag, false);
            }

            return Task.CompletedTask;
        }

        private string GetQueueName(string boundedContext, bool useFanoutMode = false)
        {
            if (useFanoutMode)
            {
                return $"{boundedContext}.{typeof(T).Name}.{Environment.MachineName}";
            }
            else
            {
                string systemMarker = typeof(ISystemHandler).IsAssignableFrom(typeof(T)) ? "cronus." : string.Empty;
                // This is the default
                return $"{boundedContext}.{systemMarker}{typeof(T).Name}";
            }
        }

        public void Stop()
        {
            connection.Close();
        }
    }
}
