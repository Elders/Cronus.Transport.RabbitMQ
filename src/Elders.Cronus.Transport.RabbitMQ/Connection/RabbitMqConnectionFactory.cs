using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory<TOptions> : IRabbitMqConnectionFactory where TOptions : IRabbitMqOptions
    {
        private readonly ILogger<RabbitMqConnectionFactory<TOptions>> logger;
        private readonly TOptions options;

        public RabbitMqConnectionFactory(IOptionsMonitor<TOptions> optionsMonitor, ILogger<RabbitMqConnectionFactory<TOptions>> logger)
        {
            options = optionsMonitor.CurrentValue;
            this.logger = logger;
        }

        public IConnection CreateConnection()
        {
            return CreateConnectionWithOptions(options);
        }

        public IConnection CreateConnectionWithOptions(IRabbitMqOptions options)
        {
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);

            bool tailRecursion = false;

            do
            {
                try
                {
                    var connectionFactory = new ConnectionFactory();
                    connectionFactory.Port = options.Port;
                    connectionFactory.UserName = options.Username;
                    connectionFactory.Password = options.Password;
                    connectionFactory.VirtualHost = options.VHost;
                    connectionFactory.DispatchConsumersAsync = true;
                    connectionFactory.AutomaticRecoveryEnabled = true;
                    connectionFactory.EndpointResolverFactory = (_) => new MultipleEndpointResolver(options);

                    return connectionFactory.CreateConnection();
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
