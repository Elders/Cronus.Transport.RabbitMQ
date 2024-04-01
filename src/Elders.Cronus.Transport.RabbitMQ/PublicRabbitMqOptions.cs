using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqOptionsCollection
    {
        public List<PublicRabbitMqOptions> PublicClustersOptions { get; set; }
    }

    public class PublicRabbitMqOptions : IRabbitMqOptions
    {
        const string BoundedContextDefault = "implicit";
        const string ServerDefault = "127.0.0.1";
        const int PortDefault = 5672;
        const string VHostDefault = "/";
        const string UsernameDefault = "guest";
        const string PasswordDefault = "guest";
        const int AdminPortDefault = 5672;

        private string server = ServerDefault;
        private string vHost = VHostDefault;
        private string boundedContext = BoundedContextDefault;

        //[Required]
        public string Server { get => server; set => server = value?.ToLower(); }

        public int Port { get; set; } = PortDefault;

        //[Required]
        public string VHost { get => vHost; set => vHost = value?.ToLower(); }

        public string Username { get; set; } = UsernameDefault;

        public string Password { get; set; } = PasswordDefault;

        public int AdminPort { get; set; } = AdminPortDefault;

        public string ApiAddress { get; set; }

        // [Required]
        public string BoundedContext { get => boundedContext; set => boundedContext = value?.ToLower(); }

        public FederatedExchangeOptions FederatedExchange { get; set; }

        public bool UseSsl { get; set; } = false;

        public IRabbitMqOptions GetOptionsFor(string boundedContext)
        {
            return this;
        }

        public IEnumerable<string> GetUpstreamUris()
        {
            if (FederatedExchange is null)
            {
                return Enumerable.Empty<string>();
            }
            else
            {
                return AmqpTcpEndpoint.ParseMultiple(FederatedExchange.UpstreamUri)
                    .Select(endpoint =>
                    {
                        endpoint.Ssl.Enabled = FederatedExchange.UseSsl;
                        return $"{endpoint}/{FederatedExchange.VHost}";
                    });
            }
        }

        private IEnumerable<string> GetDefaultUpstreamUri()
        {
            yield return $"amqp://{Username}:{Password}@localhost:{PortDefault}/{VHost}";
        }
    }

    public class FederatedExchangeOptions
    {
        [Required]
        public string UpstreamUri { get; set; }
        [Required]
        public string VHost { get; set; }
        public bool UseSsl { get; set; } = false;
        public int MaxHops { get; set; } = 1;
    }

    public class PublicRabbitMqOptionsProvider : CronusOptionsProviderBase<PublicRabbitMqOptionsCollection>
    {
        public const string SettingKey = "cronus:transport:publicrabbitmq";

        public PublicRabbitMqOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(PublicRabbitMqOptionsCollection options)
        {
            options.PublicClustersOptions = new List<PublicRabbitMqOptions>();
            List<PublicRabbitMqOptions> cfg = configuration.GetRequiredSection(SettingKey).Get<List<PublicRabbitMqOptions>>();

            options.PublicClustersOptions.AddRange(cfg);
        }
    }
}
