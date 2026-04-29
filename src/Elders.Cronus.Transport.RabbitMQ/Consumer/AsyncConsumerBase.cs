using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class AsyncConsumerBase : AsyncEventingBasicConsumer
    {
        protected readonly ILogger logger;
        protected readonly ISerializer serializer;
        protected readonly IChannel channel;
        private bool isСurrentlyConsuming;

        public AsyncConsumerBase(IChannel channel, ISerializer serializer, ILogger logger) : base(channel)
        {
            ArgumentNullException.ThrowIfNull(channel);
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(logger);

            this.channel = channel;
            this.serializer = serializer;
            this.logger = logger;
            isСurrentlyConsuming = false;
            ReceivedAsync += AsyncListener_Received;
        }

        protected abstract Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer);

        /// <summary>
        /// Stops the consumer, detaching the receive listener and waiting for any in-flight messages to finish.
        /// </summary>
        public async Task StopAsync()
        {
            // 1. We detach the listener so ther will be no new messages coming from the queue
            ReceivedAsync -= AsyncListener_Received;

            // 2. Wait to handle any messages in progress
            while (isСurrentlyConsuming)
            {
                // We are trying to wait all consumers to finish their current work.
                // Ofcourse the host could be forcibly shut down but we are doing our best.

                await Task.Delay(10).ConfigureAwait(false);
            }

            if (channel.IsOpen)
                await channel.AbortAsync().ConfigureAwait(false);
        }

        private async Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            using (logger.BeginScope(s =>
            {
                if (@event.BasicProperties.IsHeadersPresent() && @event.BasicProperties.Headers.TryGetValue("cronus_messageid", out object messageIdBytes))
                {
                    var messageId = new Guid((byte[])messageIdBytes);
                    s.AddScope("cronus_messageid", messageId.ToString());
                }
            }))
            {
                try
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Message received. Sender {sender}.", sender.GetType().Name);
                    isСurrentlyConsuming = true;

                    if (sender is AsyncEventingBasicConsumer consumer)
                        await DeliverMessageToSubscribersAsync(@event, consumer).ConfigureAwait(false);
                }
                catch (Exception ex) when (True(() => logger.LogError(ex, $"Failed to deliver message. MesasgeData: {Convert.ToBase64String(@event.Body.ToArray())}")))
                {
                    throw;
                }
                finally
                {
                    isСurrentlyConsuming = false;
                }
            }
        }
    }

    public class AsyncConsumerBase<TSubscriber> : AsyncConsumerBase
    {
        private readonly ISubscriberCollection<TSubscriber> subscriberCollection;

        public AsyncConsumerBase(IChannel channel, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ILogger logger) : base(channel, serializer, logger)
        {
            ArgumentNullException.ThrowIfNull(subscriberCollection);

            this.subscriberCollection = subscriberCollection;
        }

        private Task SafeProcessAsync(ISubscriber subscriber, CronusMessage cronusMessage)
        {
            try
            {
                return subscriber.ProcessAsync(cronusMessage);
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected override async Task DeliverMessageToSubscribersAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            List<Task> deliverTasks = new List<Task>();
            try
            {
                cronusMessage = serializer.DeserializeFromBytes<CronusMessage>(ev.Body.ToArray());
                cronusMessage = ExpandRawPayload(cronusMessage);
            }
            catch (Exception ex)
            {
                // TODO: send to dead letter exchange/queue
                logger.LogError(ex, "Failed to process message. Failed to deserialize: {}", Convert.ToBase64String(ev.Body.ToArray()));
                await AckAsync(ev, consumer).ConfigureAwait(false);
                return;
            }

            var subscribers = subscriberCollection.GetInterestedSubscribers(cronusMessage);

            try
            {
                foreach (var subscriber in subscribers)
                {
                    deliverTasks.Add(SafeProcessAsync(subscriber, cronusMessage));
                }

                await Task.WhenAll(deliverTasks).ConfigureAwait(false);
            }
            catch (Exception ex) when (True(() =>
                {
                    // Try find some errors
                    StringBuilder subscriberErrors = new StringBuilder();
                    foreach (Task subscriberCompletedTasks in deliverTasks)
                    {
                        if (subscriberCompletedTasks.IsFaulted)
                        {
                            subscriberErrors.AppendLine(subscriberCompletedTasks.Exception.ToString());
                        }
                    }
                    logger.LogError(ex, "Failed to process {cronus_messageType} {@cronus_messageData}. Actual errors {errors}", cronusMessage.GetMessageType(), cronusMessage, subscriberErrors.ToString());
                }
            ))
            { }
            finally
            {
                await AckAsync(ev, consumer).ConfigureAwait(false);
            }

            static async Task AckAsync(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
            {
                if (consumer.Channel.IsOpen)
                {
                    await consumer.Channel.BasicAckAsync(ev.DeliveryTag, false).ConfigureAwait(false);
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
