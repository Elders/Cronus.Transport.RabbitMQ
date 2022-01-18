using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class AsyncConsumerFactory<T>
    {
        private readonly ILogger logger = CronusLogger.CreateLogger(typeof(AsyncConsumerFactory<>));
        private readonly ISerializer serializer;
        private readonly RabbitMqConsumerOptions consumerOptions;
        private readonly BoundedContext boundedContext;
        private readonly ISubscriberCollection<T> subscriberCollection;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private IConnection connection;

        public AsyncConsumerFactory(IOptionsMonitor<RabbitMqConsumerOptions> consumerOptions, IOptionsMonitor<BoundedContext> boundedContext, ISerializer serializer, ISubscriberCollection<T> subscriberCollection, IRabbitMqConnectionFactory connectionFactory)
        {
            this.boundedContext = boundedContext.CurrentValue;
            this.serializer = serializer;
            this.consumerOptions = consumerOptions.CurrentValue;
            this.subscriberCollection = subscriberCollection;
            this.connectionFactory = connectionFactory;
        }

        public void CreateConsumers()
        {
            connection = connectionFactory.CreateConnection();
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

        public void Stop()
        {
            connection.Close();
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            if (sender is AsyncEventingBasicConsumer consumer)
            {
                return DeliverMessageToSubscribers(@event, consumer);
            }

            logger.Error(() => $"There is no consumer for {(@event.Body.ToArray())}");

            return Task.CompletedTask;
        }

        private Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            var cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(ev.Body);
            try
            {
                var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);
                foreach (var subscriber in subscribers)
                {
                    subscriber.Process(cronusMessage);
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + MessageAsString(cronusMessage))) { }
            finally
            {
                consumer.Model.BasicAck(ev.DeliveryTag, false);
            }

            return Task.CompletedTask;
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
    }
}
