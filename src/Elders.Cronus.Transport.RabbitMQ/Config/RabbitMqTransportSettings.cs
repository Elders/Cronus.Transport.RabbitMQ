using System;
using Elders.Cronus.IocContainer;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Config
{
    public interface IRabbitMqTransportSettings : IPipelineTransportSettings
    {
        string Server { get; set; }
        int Port { get; set; }
        int AdminPort { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        string VirtualHost { get; set; }
    }

    public class RabbitMqTransportSettings : SettingsBuilder, IRabbitMqTransportSettings
    {
        public RabbitMqTransportSettings(ISettingsBuilder settingsBuilder) : base(settingsBuilder)
        {
            this
                .WithDefaultConnectionSettings(); //each endpoint will have separate thread
        }

        string IRabbitMqTransportSettings.Password { get; set; }

        int IRabbitMqTransportSettings.Port { get; set; }

        int IRabbitMqTransportSettings.AdminPort { get; set; }

        string IRabbitMqTransportSettings.Server { get; set; }

        string IRabbitMqTransportSettings.Username { get; set; }

        string IRabbitMqTransportSettings.VirtualHost { get; set; }

        IEndpointNameConvention IPipelineTransportSettings.EndpointNameConvention { get; set; }

        IPipelineNameConvention IPipelineTransportSettings.PipelineNameConvention { get; set; }

        public override void Build()
        {
            var builder = this as ISettingsBuilder;
            builder.Container.RegisterSingleton<IPipelineTransport>(() =>
            {
                var serializer = builder.Container.Resolve<ISerializer>();
                return new RabbitMqTransport(serializer, this as IRabbitMqTransportSettings);
            }, builder.Name);
        }
    }

    public static class RabbitMqTransportExtensions
    {
        public static T UseRabbitMqTransport<T>(
            this T self,
            Action<IRabbitMqTransportSettings> configure = null,
            Action<IPipelineTransportSettings> configureConventions = null)
            where T : ISettingsBuilder
        {
            RabbitMqTransportSettings settings = new RabbitMqTransportSettings(self as ISettingsBuilder);
            settings
                .WithDefaultConnectionSettings()
                .WithEndpointPerBoundedContext();

            if (configure != null) configure(settings);
            if (configureConventions != null) configureConventions(settings);


            (settings as ISettingsBuilder).Build();

            return self;
        }

        public static T WithDefaultConnectionSettings<T>(this T self) where T : IRabbitMqTransportSettings
        {
            self.Server = "localhost";
            self.Port = 5672;
            self.AdminPort = 15672;
            self.Username = ConnectionFactory.DefaultUser;
            self.Password = ConnectionFactory.DefaultPass;
            self.VirtualHost = ConnectionFactory.DefaultVHost;
            return self;
        }
    }
}
