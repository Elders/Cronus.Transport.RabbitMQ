using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory<TOptions> : ConnectionFactory
        where TOptions : IRabbitMqOptions
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactory<TOptions>));

        private readonly TOptions options;

        public RabbitMqConnectionFactory(IOptionsMonitor<TOptions> settings)
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
