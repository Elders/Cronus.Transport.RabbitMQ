using System;
using Elders.Cronus.IocContainer;
using Elders.Cronus.Pipeline.Config;
using RabbitMQ.Client;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Config
{
    public interface IRabbitMqTransportSettings : ISettingsBuilder
    {
        string Server { get; set; }
        int Port { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        string VirtualHost { get; set; }
    }

    public class RabbitMqTransportSettings : SettingsBuilder, IRabbitMqTransportSettings
    {
        public RabbitMqTransportSettings(ISettingsBuilder settingsBuilder) : base(settingsBuilder)
        {
            this.WithDefaultConnectionSettings();
        }

        public override void Build()
        {
            var builder = this as ISettingsBuilder;
            var endpointNameConvention = builder.Container.Resolve<IEndpointNameConvention>(builder.Name);
            var pipelineNameConvention = builder.Container.Resolve<IPipelineNameConvention>(builder.Name);
            builder.Container.RegisterSingleton<IPipelineTransport>(() => new RabbitMqTransport(this as IRabbitMqTransportSettings, pipelineNameConvention, endpointNameConvention));
        }

        string IRabbitMqTransportSettings.Password { get; set; }

        int IRabbitMqTransportSettings.Port { get; set; }

        string IRabbitMqTransportSettings.Server { get; set; }

        string IRabbitMqTransportSettings.Username { get; set; }

        string IRabbitMqTransportSettings.VirtualHost { get; set; }
    }

    public static class RabbitMqTransportExtensions
    {
        public static T UseRabbitMqTransport<T>(this T self, Action<RabbitMqTransportSettings> configure = null)
        {
            RabbitMqTransportSettings settings = new RabbitMqTransportSettings(self as ISettingsBuilder);
            if (configure != null)
                configure(settings);
            (settings as ISettingsBuilder).Build();

            return self;
        }

        public static T WithDefaultConnectionSettings<T>(this T self) where T : IRabbitMqTransportSettings
        {
            self.Server = "localhost";
            self.Port = 5672;
            self.Username = ConnectionFactory.DefaultUser;
            self.Password = ConnectionFactory.DefaultPass;
            self.VirtualHost = ConnectionFactory.DefaultVHost;
            return self;
        }
    }
}