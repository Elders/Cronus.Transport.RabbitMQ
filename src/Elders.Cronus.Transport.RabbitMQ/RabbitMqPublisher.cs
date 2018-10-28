﻿using System;
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

        bool stoped = false;

        private readonly ISerializer serializer;
        private static IConnection connection;
        private readonly IConnectionFactory connectionFactory;
        private IModel publishModel;

        private readonly string boundedContext;


        public RabbitMqPublisher(IConfiguration configuration, ISerializer serializer, IConnectionFactory connectionFactory, ITenantResolver tenantResolver)
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

                Close();

                return false;
            }
        }

        private void Close()
        {
            stoped = true;
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
