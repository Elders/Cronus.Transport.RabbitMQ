using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqOptions : IRabbitMqOptions
    {
        const string BoundedContextDefault = "implicit";
        const string ServerDefault = "127.0.0.1";
        const int PortDefault = 5672;
        const string VHostDefault = "/";
        const string UsernameDefault = "guest";
        const string PasswordDefault = "guest";
        const int AdminPortDefault = 5672;

        public string BoundedContext { get; set; } = BoundedContextDefault;

        public bool UseAsyncDispatcher { get; set; } = false;

        public string Server { get; set; } = ServerDefault;

        public int Port { get; set; } = PortDefault;

        public string VHost { get; set; } = VHostDefault;

        public string Username { get; set; } = UsernameDefault;

        public string Password { get; set; } = PasswordDefault;

        public int AdminPort { get; set; } = AdminPortDefault;

        public string ApiAddress { get; set; }

        public FederatedExchangeOptions FederatedExchange { get; set; }

        List<RabbitMqOptions> ExternalServices { get; set; }

        public IRabbitMqOptions GetOptionsFor(string boundedContext)
        {
            var fromCfg = ExternalServices?.Where(opt => opt.BoundedContext.Equals(boundedContext, System.StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (fromCfg is null == false)
            {
                if (fromCfg.Server.Equals(ServerDefault, System.StringComparison.OrdinalIgnoreCase)) fromCfg.Server = Server;
                if (fromCfg.Port == PortDefault) fromCfg.Port = Port;
                if (fromCfg.VHost.Equals(VHostDefault, System.StringComparison.OrdinalIgnoreCase)) fromCfg.VHost = VHost;
                if (fromCfg.Username.Equals(UsernameDefault, System.StringComparison.OrdinalIgnoreCase)) fromCfg.Username = Username;
                if (fromCfg.Password.Equals(PasswordDefault, System.StringComparison.OrdinalIgnoreCase)) fromCfg.Password = Password;
                if (fromCfg.AdminPort == AdminPortDefault) fromCfg.AdminPort = AdminPort;
                if (string.IsNullOrEmpty(fromCfg.ApiAddress)) fromCfg.ApiAddress = ApiAddress;

                return fromCfg;
            }

            return this;
        }
    }

    public class RabbitMqOptionsProvider : CronusOptionsProviderBase<RabbitMqOptions>
    {
        public const string SettingKey = "cronus:transport:rabbitmq";

        public RabbitMqOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(RabbitMqOptions options)
        {
            configuration.GetSection(SettingKey).Bind(options, opt => opt.BindNonPublicProperties = true);
        }
    }
}
