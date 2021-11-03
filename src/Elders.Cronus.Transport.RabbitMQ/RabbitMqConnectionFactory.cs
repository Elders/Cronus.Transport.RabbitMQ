using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory<TOptions> : ConnectionFactory
        where TOptions : IRabbitMqOptions
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactory<TOptions>));

        private readonly TOptions options;
        private readonly RabbitMqInfrastructure rabbitMqInfrastructure;

        public RabbitMqConnectionFactory(RabbitMqInfrastructure rabbitMqInfrastructure, IOptionsMonitor<TOptions> settings)
        {
            options = settings.CurrentValue;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            HostName = options.Server;
            Port = options.Port;
            UserName = options.Username;
            Password = options.Password;
            VirtualHost = options.VHost;
            AutomaticRecoveryEnabled = false;
            EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
            this.rabbitMqInfrastructure = rabbitMqInfrastructure;
        }

        public override IConnection CreateConnection()
        {
            try
            {
                return base.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
            }
            catch (BrokerUnreachableException)
            {
                Thread.Sleep(1000);
                rabbitMqInfrastructure.Initialize();
                return base.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
            }
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }

    public class RabbitMqConnectionFactoryNew : ConnectionFactory
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactoryNew));

        private readonly IRabbitMqOptions options;

        public RabbitMqConnectionFactoryNew(IRabbitMqOptions options)
        {
            this.options = options;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            HostName = options.Server;
            Port = options.Port;
            UserName = options.Username;
            Password = options.Password;
            VirtualHost = options.VHost;
            AutomaticRecoveryEnabled = false;
            EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
        }

        public override IConnection CreateConnection()
        {
            return base.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }
}
