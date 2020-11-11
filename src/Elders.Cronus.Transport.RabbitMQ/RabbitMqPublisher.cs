using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionResolver<out TOptions> : IDisposable
        where TOptions : IRabbitMqOptions
    {
        IConnection Resolve(CronusMessage message);
    }

    public sealed class RabbitMqConnectionResolver<TOptions> : IRabbitMqConnectionResolver<TOptions>
        where TOptions : IRabbitMqOptions
    {
        TOptions options;

        ConcurrentDictionary<string, IConnection> connections;

        public RabbitMqConnectionResolver(IOptionsMonitor<TOptions> monitor)
        {
            options = monitor.CurrentValue;
            connections = new ConcurrentDictionary<string, IConnection>();
        }

        public IConnection Resolve(CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            IConnection connection = connections.GetOrAdd(boundedContext, (bc, opt) => GetConnection(bc, opt), options);

            if (connection is null || connection.IsOpen == false)
            {
                connection?.Abort(5000);
                connection = GetConnection(boundedContext, options);
                connections.TryAdd(boundedContext, connection);
            }

            return connection;
        }

        private IConnection GetConnection(string boundedContext, TOptions options)
        {
            IRabbitMqOptions currentOptions = options.GetOptionsFor(boundedContext);

            var connectionFactory = new RabbitMqConnectionFactoryNew(currentOptions);
            var connection = connectionFactory.CreateConnection();
            connection.AutoClose = false;

            return connection;
        }

        public void Dispose()
        {
            foreach (IConnection connection in connections.Select(x => x.Value))
            {
                connection?.Abort(5000);
                connection.Dispose();
            }

            connections.Clear();
        }
    }

    public abstract class RabbitMqPublisher<TMessage> : Publisher<TMessage>, IDisposable where TMessage : IMessage
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqPublisher<>));

        bool isStopped = false;

        private readonly ISerializer serializer;
        private readonly IRabbitMqConnectionResolver<IRabbitMqOptions> connectionResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private IModel publishModel;

        public RabbitMqPublisher(ISerializer serializer, IRabbitMqConnectionResolver<IRabbitMqOptions> connectionResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqNamer rabbitMqNamer)
            : base(tenantResolver, boundedContext.CurrentValue)
        {
            this.serializer = serializer;
            this.connectionResolver = connectionResolver;
            this.rabbitMqNamer = rabbitMqNamer;
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

                string boundedContext = message.Headers[MessageHeader.BoundedContext];
                IConnection connection = connectionResolver.Resolve(message);

                if (publishModel == null || publishModel.IsClosed)
                    publishModel = connection.CreateModel();

                IBasicProperties props = publishModel.CreateBasicProperties();
                props.Headers = new Dictionary<string, object>() { { message.Payload.GetType().GetContractId(), boundedContext } };

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
                lock (connectionResolver)
                {
                    publishModel?.Abort();
                    publishModel = null;
                }
                return false;
            }
        }

        private void Close()
        {
            isStopped = true;
            lock (connectionResolver)
            {
                publishModel?.Abort();
                publishModel = null;

                connectionResolver.Dispose();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
