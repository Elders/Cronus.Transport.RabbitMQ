using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionFactory
    {
        IConnection CreateConnection();
        IConnection CreateConnectionWithChannels(int workers, out List<IModel> channels);
        IConnection CreateConnectionWithOptions(IRabbitMqOptions options);
    }

    public class RabbitMqConnectionFactory<TOptions> : IRabbitMqConnectionFactory where TOptions : IRabbitMqOptions
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactory<TOptions>));
        private readonly TOptions options;
        private readonly RabbitMqInfrastructure rabbitMqInfrastructure;
        private readonly ConnectionFactory connectionFactory;

        public RabbitMqConnectionFactory(RabbitMqInfrastructure rabbitMqInfrastructure, IOptionsMonitor<TOptions> settings, ConnectionFactory connectionFactory)
        {
            options = settings.CurrentValue;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            this.rabbitMqInfrastructure = rabbitMqInfrastructure;
            this.connectionFactory = connectionFactory;
            this.connectionFactory.HostName = options.Server;
            this.connectionFactory.Port = options.Port;
            this.connectionFactory.UserName = options.Username;
            this.connectionFactory.Password = options.Password;
            this.connectionFactory.VirtualHost = options.VHost;
            this.connectionFactory.DispatchConsumersAsync = true;
            this.connectionFactory.AutomaticRecoveryEnabled = true;
            this.connectionFactory.EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
        }

        public IConnection CreateConnection()
        {
            return CreateConnectionWithOptions(options);
        }

        public IConnection CreateConnectionWithChannels(int workersCount, out List<IModel> channels)
        {
            channels = new List<IModel>();
            var connection = CreateConnectionWithOptions(options);

            try
            {
                if (connection is not null && connection.IsOpen)
                {
                    for (int i = 0; i < workersCount; i++)
                    {
                        var channel = connection.CreateModel();
                        channel.ConfirmSelect();
                        channels.Add(channel);
                    }
                    return connection;
                }
            }
            catch (BrokerUnreachableException ex) when (logger.WarnException(ex, () => $"Failed to create channels to RabbitMQ. Retrying..."))
            {
                rabbitMqInfrastructure.Initialize();
            }

            return CreateConnectionWithChannels(workersCount, out channels);
        }

        public IConnection CreateConnectionWithOptions(IRabbitMqOptions options)
        {
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);

            try
            {
                var newConnectionFactory = new ConnectionFactory();
                newConnectionFactory.Port = options.Port;
                newConnectionFactory.UserName = options.Username;
                newConnectionFactory.Password = options.Password;
                newConnectionFactory.VirtualHost = options.VHost;
                newConnectionFactory.DispatchConsumersAsync = true;
                newConnectionFactory.AutomaticRecoveryEnabled = true;
                newConnectionFactory.EndpointResolverFactory = (_) => new MultipleEndpointResolver(options);
                return newConnectionFactory.CreateConnection();
            }
            catch (BrokerUnreachableException ex) when (logger.WarnException(ex, () => $"Failed to create RabbitMQ connection with options {nameof(options)}. Retrying..."))
            {
                rabbitMqInfrastructure.Initialize();
            }

            return CreateConnectionWithOptions(options);
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {

            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }
}
