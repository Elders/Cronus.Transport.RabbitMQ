using System;
using System.Collections.Generic;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqPublisher<TMessage> : Publisher<TMessage>, IDisposable where TMessage : IMessage
    {
        bool stoped = false;

        private readonly ISerializer serializer;

        private static IConnection connection;

        private readonly IConnectionFactory connectionFactory;

        private IModel publishModel;

        public RabbitMqPublisher(ISerializer serializer, IConnectionFactory connectionFactory)
        {
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                if (stoped)
                    return false;

                if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                {
                    lock (connectionFactory)
                    {
                        if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                        {
                            connection?.Abort();
                            connection = connectionFactory.CreateConnection();
                            connection.AutoClose = false;
                        }
                    }
                }

                if (publishModel == null || publishModel.IsClosed)
                    publishModel = connection.CreateModel();

                IBasicProperties props = publishModel.CreateBasicProperties();
                props.Headers = new Dictionary<string, object>() { { message.Payload.GetType().GetContractId(), string.Empty } };

                props.Persistent = true;
                props.Priority = 9;

                byte[] body = this.serializer.SerializeToBytes(message);

                var publishDelayInMiliseconds = message.GetPublishDelay();
                if (publishDelayInMiliseconds < 1000)
                {
                    var exchangeName = RabbitMqNamer.GetExchangeName(message.Payload.GetType());
                    publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                }
                else
                {
                    var exchangeName = RabbitMqNamer.GetExchangeName(message.Payload.GetType()) + ".Scheduler";
                    props.Headers.Add("x-delay", message.GetPublishDelay());
                    publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                }
                return true;
            }
            catch (Exception ex)
            {
                Close();

                return false;
            }
        }

        private void Stop()
        {
            Close();
            stoped = true;
        }

        private void Close()
        {
            publishModel?.Abort();
            connection?.Abort();

            connection = null;
            publishModel = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
