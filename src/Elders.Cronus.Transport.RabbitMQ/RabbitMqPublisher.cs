using System;
using System.Collections.Generic;
using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisher<TMessage> : Publisher<TMessage>, IDisposable where TMessage : IMessage
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqPublisher<>));

        bool isStopped = false;

        private readonly ISerializer serializer;
        private static IConnection connection;
        private readonly IConnectionFactory connectionFactory;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private IModel publishModel;

        public RabbitMqPublisher(ISerializer serializer, IConnectionFactory connectionFactory, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqNamer rabbitMqNamer)
            : base(tenantResolver, boundedContext.CurrentValue)
        {
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        protected void EnsureConnected()
        {
            if (connection is null || connection.IsOpen == false)
            {
                lock (connectionFactory)
                {
                    if (connection is null || connection.IsOpen == false)
                    {
                        connection?.Abort();
                        connection = connectionFactory.CreateConnection();
                        connection.AutoClose = false;
                    }
                }
            }
        }

        protected TResult Do<TResult>(Func<IConnection, TResult> action)
        {
            try
            {
                EnsureConnected();
                return action(connection);
            }
            catch (Exception ex)
            {
                logger.WarnException(ex, () => ex.Message);
                lock (connectionFactory)
                {
                    connection?.Abort(5000);
                    connection = null;
                }
                return default;
            }
        }

        protected void Do(Action<IConnection> action)
        {
            Do(con =>
            {
                action(con);
                return true;
            });
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                if (isStopped)
                {
                    logger.Warn(() => "Failed to publish a message. Publisher is stopped/disposed.");
                    return false;
                }

                EnsureConnected();

                if (publishModel == null || publishModel.IsClosed)
                    publishModel = connection.CreateModel();

                IBasicProperties props = publishModel.CreateBasicProperties();
                props.Headers = new Dictionary<string, object>() { { message.Payload.GetType().GetContractId(), message.Headers[MessageHeader.BoundedContext] } };

                props.Persistent = true;
                props.Priority = 9;

                byte[] body = this.serializer.SerializeToBytes(message);

                var publishDelayInMiliseconds = message.GetPublishDelay();
                if (publishDelayInMiliseconds < 1000)
                {
                    foreach (var exchange in rabbitMqNamer.GetExchangeNames(message.Payload.GetType()))
                    {
                        publishModel.BasicPublish(exchange, string.Empty, false, props, body);
                    }
                }
                else
                {
                    foreach (var exchange in rabbitMqNamer.GetExchangeNames(message.Payload.GetType()))
                    {
                        var exchangeName = $"{exchange}.Scheduler";
                        props.Headers.Add("x-delay", message.GetPublishDelay());
                        publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.WarnException(ex, () => ex.Message);
                lock (connectionFactory)
                {
                    publishModel?.Abort();
                    publishModel = null;

                    connection?.Abort(5000);
                    connection = null;
                }
                return false;
            }
        }

        private void Close()
        {
            isStopped = true;
            lock (connectionFactory)
            {
                publishModel?.Abort();
                connection?.Abort(5000);

                connection = null;
                publishModel = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
