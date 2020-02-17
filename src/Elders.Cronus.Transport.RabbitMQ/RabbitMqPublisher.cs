using System;
using System.Collections.Generic;
using Elders.Cronus.Multitenancy;
using Elders.Cronus.Transport.RabbitMQ.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqPublisher<TMessage> : Publisher<TMessage>, IDisposable where TMessage : IMessage
    {
        static readonly ILog log = LogProvider.GetLogger(typeof(RabbitMqPublisher<>));

        bool isStopped = false;

        private readonly ISerializer serializer;
        private static IConnection connection;
        private readonly IConnectionFactory connectionFactory;
        private IModel publishModel;

        private readonly string boundedContext;


        public RabbitMqPublisher(IConfiguration configuration, ISerializer serializer, IConnectionFactory connectionFactory, ITenantResolver<IMessage> tenantResolver)
            : base(tenantResolver)
        {
            this.boundedContext = configuration["cronus_boundedcontext"];
            this.serializer = serializer;
            this.connectionFactory = connectionFactory;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                if (isStopped)
                {
                    log.Warn("Failed to publish a message. Publisher is stopped/disposed.");
                    return false;
                }

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
                    var exchangeName = RabbitMqNamer.GetExchangeName(boundedContext, message.Payload.GetType());
                    publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                }
                else
                {
                    var exchangeName = RabbitMqNamer.GetExchangeName(boundedContext, message.Payload.GetType()) + ".Scheduler";
                    props.Headers.Add("x-delay", message.GetPublishDelay());
                    publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                }
                return true;
            }
            catch (Exception ex)
            {
                log.WarnException(ex.Message, ex);
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
            publishModel?.Abort();
            connection?.Abort();

            connection = null;
            publishModel = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
