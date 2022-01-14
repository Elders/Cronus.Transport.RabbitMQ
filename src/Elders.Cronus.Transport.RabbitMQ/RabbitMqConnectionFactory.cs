using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory<TOptions> where TOptions : IRabbitMqOptions
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
            this.connectionFactory.AutomaticRecoveryEnabled = false;
            this.connectionFactory.EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
        }

        public IConnection CreateConnection()
        {
            try
            {
                return this.connectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
            }
            catch (BrokerUnreachableException)
            {
                Thread.Sleep(1000);
                rabbitMqInfrastructure.Initialize();
                return this.connectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());

            }
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }

    public class RabbitMqConnectionFactoryNew
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactoryNew));

        private readonly IRabbitMqOptions options;
        private readonly ConnectionFactory connectionFactory;

        public RabbitMqConnectionFactoryNew(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public RabbitMqConnectionFactoryNew(IRabbitMqOptions options)
        {
            this.options = options;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            this.connectionFactory.HostName = options.Server;
            this.connectionFactory.Port = options.Port;
            this.connectionFactory.UserName = options.Username;
            this.connectionFactory.Password = options.Password;
            this.connectionFactory.VirtualHost = options.VHost;
            this.connectionFactory.AutomaticRecoveryEnabled = false;
            this.connectionFactory.EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
        }

        public IConnection CreateConnection()
        {
            return this.connectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }
}
