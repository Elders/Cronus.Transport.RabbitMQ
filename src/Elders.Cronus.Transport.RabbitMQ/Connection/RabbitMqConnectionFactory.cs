using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private readonly ILogger<RabbitMqConnectionFactory<TOptions>> logger;
        private readonly TOptions options;
        private readonly ConnectionFactory connectionFactory;

        public RabbitMqConnectionFactory(IOptionsMonitor<TOptions> settings, ConnectionFactory connectionFactory, ILogger<RabbitMqConnectionFactory<TOptions>> logger)
        {
            this.logger = logger;
            options = settings.CurrentValue;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
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

            bool tailRecursion = false;

            do
            {
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
                catch (Exception ex)
                {
                    tailRecursion = true;
                    if (ex is BrokerUnreachableException)
                        logger.Warn(() => $"Failed to create RabbitMQ connection with channels. Retrying...");
                    else
                        logger.WarnException(ex, () => $"Failed to create RabbitMQ connection with channels. Retrying...");
                }
            } while (tailRecursion == true);


            return default;
        }

        public IConnection CreateConnectionWithOptions(IRabbitMqOptions options)
        {
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);

            bool tailRecursion = false;

            do
            {
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
                catch (Exception ex)
                {
                    if (ex is BrokerUnreachableException)
                        logger.Warn(() => $"Failed to create RabbitMQ connection. Retrying...");
                    else
                        logger.WarnException(ex, () => $"Failed to create RabbitMQ connection. Retrying...");

                    Task.Delay(5000).GetAwaiter().GetResult();
                    tailRecursion = true;
                }
            } while (tailRecursion == true);

            return default;
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }
}
