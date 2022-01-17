using System;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    public class DeprecatedDefaultBasicConsumer : IBasicConsumer
    {
        public readonly object m_eventLock = new object();
        public EventHandler<ConsumerEventArgs> m_consumerCancelled;

        public DeprecatedDefaultBasicConsumer()
        {
            ShutdownReason = null;
            Model = null;
            IsRunning = false;
            ConsumerTag = null;
        }

        public DeprecatedDefaultBasicConsumer(IModel model)
        {
            ShutdownReason = null;
            IsRunning = false;
            ConsumerTag = null;
            Model = model;
        }

        public string ConsumerTag { get; set; }

        public bool IsRunning { get; protected set; }

        public ShutdownEventArgs ShutdownReason { get; protected set; }

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


        public virtual void HandleBasicDeliver(string consumerTag,
             ulong deliveryTag,
             bool redelivered,
             string exchange,
             string routingKey,
             IBasicProperties properties,
             byte[] body)
        {
            // Nothing to do here.
        }

        public virtual void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            // Nothing to do here.
        }

        public virtual void HandleBasicCancel(string consumerTag)
        {
            OnCancel();
        }

        public virtual void HandleBasicCancelOk(string consumerTag)
        {
            OnCancel();
        }

        public virtual void HandleBasicConsumeOk(string consumerTag)
        {
            ConsumerTag = consumerTag;
            IsRunning = true;
        }

        public virtual void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            OnCancel();
        }

        public virtual void OnCancel()
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
        }
    }
}
