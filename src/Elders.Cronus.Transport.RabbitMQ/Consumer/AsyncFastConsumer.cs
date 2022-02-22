using Elders.Cronus.MessageProcessing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ
{
    /// <summary>
    /// Transient consumer which consumes messages with autoacknowledging and sends message to handlers without workflow chains.
    /// We use this consumer for non-important types of messages.
    /// </summary>
    /// <typeparam name="TSubscriber"></typeparam>
    public class AsyncFastConsumer<TSubscriber> : AsyncConsumerBase<TSubscriber>
    {
        private readonly ISerializer _serializer;
        private readonly ISubscriberCollection<TSubscriber> _subscribers;

        public AsyncFastConsumer(string queue, IModel model, ISubscriberCollection<TSubscriber> subscriberCollection, ISerializer serializer, ISubscriberCollection<TSubscriber> subscribers) : base(model, subscriberCollection, serializer)
        {
            _serializer = serializer;
            _subscribers = subscribers;
            model.BasicConsume(queue, true, string.Empty, this);
        }

        protected override async Task DeliverMessageToSubscribers(BasicDeliverEventArgs ev, AsyncEventingBasicConsumer consumer)
        {
            CronusMessage cronusMessage = null;
            try
            {
                cronusMessage = (CronusMessage)_serializer.DeserializeFromBytes(ev.Body);
                var subscribers = _subscribers.GetInterestedSubscribers(cronusMessage);
                foreach (var subscriber in subscribers)
                {
                    var context = new HandleContext(cronusMessage, typeof(TSubscriber));
                    MessageHandleWorkflow messageHandleWorkflow = new MessageHandleWorkflow(new CreateScopedHandlerWorkflow());

                    using (IHandlerInstance handlerInstance = messageHandleWorkflow.CreateHandler.Run(context))
                    {
                        dynamic handler = handlerInstance;
                        handler.Handle((dynamic)context.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Message loosed - ok.
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}


