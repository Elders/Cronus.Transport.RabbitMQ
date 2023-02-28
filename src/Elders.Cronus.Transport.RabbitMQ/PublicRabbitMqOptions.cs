using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqOptions : IRabbitMqOptions
    {
        const string BoundedContextDefault = "implicit";
        const string ServerDefault = "127.0.0.1";
        const int PortDefault = 5672;
        const string VHostDefault = "/";
        const string UsernameDefault = "guest";
        const string PasswordDefault = "guest";
        const int AdminPortDefault = 5672;

        public string Server { get; set; } = ServerDefault;

        public int Port { get; set; } = PortDefault;

        public string VHost { get; set; } = VHostDefault;

        public string Username { get; set; } = UsernameDefault;

        public string Password { get; set; } = PasswordDefault;

        public int AdminPort { get; set; } = AdminPortDefault;

        public string ApiAddress { get; set; }

        public string BoundedContext { get; set; } = BoundedContextDefault;

        public bool UseAsyncDispatcher { get; set; }

        public FederatedExchangeOptions FederatedExchange { get; set; } = new FederatedExchangeOptions();

        internal List<PublicRabbitMqOptions> ExternalServers { get; set; }

        public IEnumerable<IRabbitMqOptions> GetOptionsFor(string boundedContext)
        {
            List<IRabbitMqOptions> options = new List<IRabbitMqOptions>() { this };

            if (ExternalServers is not null && ExternalServers.Any())
            {
                foreach (var srvOpt in ExternalServers.Where(opt => opt.BoundedContext.Equals(boundedContext, System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (srvOpt.Server.Equals(ServerDefault, System.StringComparison.OrdinalIgnoreCase)) srvOpt.Server = Server;
                    if (srvOpt.Port == PortDefault) srvOpt.Port = Port;
                    if (srvOpt.VHost.Equals(VHostDefault, System.StringComparison.OrdinalIgnoreCase)) srvOpt.VHost = VHost;
                    if (srvOpt.Username.Equals(UsernameDefault, System.StringComparison.OrdinalIgnoreCase)) srvOpt.Username = Username;
                    if (srvOpt.Password.Equals(PasswordDefault, System.StringComparison.OrdinalIgnoreCase)) srvOpt.Password = Password;
                    if (srvOpt.AdminPort == AdminPortDefault) srvOpt.AdminPort = AdminPort;
                    if (string.IsNullOrEmpty(srvOpt.ApiAddress)) srvOpt.ApiAddress = ApiAddress;

                    options.Add(srvOpt);
                }
            }

            return options;
        }

        public IEnumerable<string> GetUpstreamUris()
        {
            return AmqpTcpEndpoint.ParseMultiple(Server).Select(x => $"{x}/{VHost}");
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
            configuration.GetSection(SettingKey).Bind(options, opt => opt.BindNonPublicProperties = true);
        }
    }
}
