using System;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client
{
    public interface IQueueingBasicConsumer
    {
        void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered,
            string exchange, string routingKey, IBasicProperties properties, byte[] body);
        void OnCancel();
        SharedQueue<BasicDeliverEventArgs> Queue { get; }
    }

    [Obsolete("Deprecated. Use EventingBasicConsumer or a different consumer interface implementation instead")]
    public class QueueingBasicConsumer : DefaultBasicConsumer, IQueueingBasicConsumer
    {
        public QueueingBasicConsumer() : this(null)
        {
        }

        public QueueingBasicConsumer(IModel model) : this(model, new SharedQueue<BasicDeliverEventArgs>())
        {
        }

        public QueueingBasicConsumer(IModel model, SharedQueue<BasicDeliverEventArgs> queue) : base(model)
        {
            Queue = queue;
        }

        public SharedQueue<BasicDeliverEventArgs> Queue { get; protected set; }

        public void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            var eventArgs = new BasicDeliverEventArgs
            {
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Redelivered = redelivered,
                Exchange = exchange,
                RoutingKey = routingKey,
                BasicProperties = properties,
                Body = body
            };
            Queue.Enqueue(eventArgs);
        }

        public void OnCancel()
        {
            base.OnCancel();
            Queue.Close();
        }
    }
}
