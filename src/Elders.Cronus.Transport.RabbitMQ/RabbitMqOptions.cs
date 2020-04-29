using Microsoft.Extensions.Configuration;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqOptions : IRabbitMqOptions
    {
        public string Server { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 5672;

        public string VHost { get; set; } = "/";

        public string Username { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public int AdminPort { get; set; } = 5672;

        public string ApiAddress { get; set; }
    }

    public class RabbitMqOptionsProvider : CronusOptionsProviderBase<RabbitMqOptions>
    {
        public const string SettingKey = "cronus:transport:rabbitmq";

        public RabbitMqOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(RabbitMqOptions options)
        {
            configuration.GetSection(SettingKey).Bind(options);
        }
    }
}
