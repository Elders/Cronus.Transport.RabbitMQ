using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace Elders.Cronus.Transport.RabbitMQ.Internal
{
    internal sealed class QueueingBasicConsumer : IQueueingBasicConsumer
    {
        public readonly object m_eventLock = new object();
        public EventHandler<ConsumerEventArgs> m_consumerCancelled;

        public QueueingBasicConsumer()
        {
            ShutdownReason = null;
            Model = null;
            IsRunning = false;
            ConsumerTag = null;
            Queue = new SharedQueue<BasicDeliverEventArgs>();
        }

        public QueueingBasicConsumer(IModel model) : this(model, new SharedQueue<BasicDeliverEventArgs>()) { }

        public QueueingBasicConsumer(IModel model, SharedQueue<BasicDeliverEventArgs> queue)
        {
            ShutdownReason = null;
            IsRunning = false;
            ConsumerTag = null;
            Model = model;
            Queue = queue;
        }

        public string ConsumerTag { get; set; }

        public bool IsRunning { get; private set; }

        public ShutdownEventArgs ShutdownReason { get; private set; }

        public SharedQueue<BasicDeliverEventArgs> Queue { get; private set; }

        public event EventHandler<ConsumerEventArgs> ConsumerCancelled
        {
            add
            {
                lock (m_eventLock)
                {
                    m_consumerCancelled += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_consumerCancelled -= value;
                }
            }
        }

        public IModel Model { get; set; }

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
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

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
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

        public void HandleBasicCancel(string consumerTag)
        {
            OnCancel();
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            OnCancel();
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            ConsumerTag = consumerTag;
            IsRunning = true;
        }

        public void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            OnCancel();
        }

        public void OnCancel()
        {
            IsRunning = false;
            EventHandler<ConsumerEventArgs> handler;
            lock (m_eventLock)
            {
                handler = m_consumerCancelled;
            }
            if (handler != null)
            {
                foreach (EventHandler<ConsumerEventArgs> h in handler.GetInvocationList())
                {
                    h(this, new ConsumerEventArgs(new string[] { ConsumerTag }));
                }
            }

            Queue.Close();
        }
    }
}
