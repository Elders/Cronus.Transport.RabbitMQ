using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqOptions : IRabbitMqOptions
    {
        public string Server { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 5672;

        public string VHost { get; set; } = "/";

        public string Username { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public int AdminPort { get; set; } = 5672;

        public string ApiAddress { get; set; }

        public bool UseAsyncDispatcher { get; set; }

        public FederatedExchangeOptions FederatedExchange { get; set; } = new FederatedExchangeOptions();

        public IRabbitMqOptions GetOptionsFor(string boundedContext)
        {
            return this;
        }

        public IEnumerable<string> GetUpstreamUris()
        {
            return AmqpTcpEndpoint.ParseMultiple(Server).Select(x => $"amqp://localhost/{VHost}");
        }
    }

    public class FederatedExchangeOptions
    {
        public int MaxHops { get; set; } = 1;
    }

    public class PublicRabbitMqOptionsProvider : CronusOptionsProviderBase<PublicRabbitMqOptions>
    {
        public const string SettingKey = "cronus:transport:publicrabbitmq";

        public PublicRabbitMqOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(PublicRabbitMqOptions options)
        {
            configuration.GetSection(SettingKey).Bind(options);
        }
    }
}
