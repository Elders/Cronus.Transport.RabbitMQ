using System;
using Elders.Cronus.IocContainer;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy;
using RabbitMQ.Client;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Config
{
    public interface IPipelineTransportSettings : ISettingsBuilder
    {
        IEndpointNameConvention EndpointNameConvention { get; set; }
        IPipelineNameConvention PipelineNameConvention { get; set; }
    }

    public interface IRabbitMqTransportSettings : IPipelineTransportSettings
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

        string IRabbitMqTransportSettings.Password { get; set; }

        int IRabbitMqTransportSettings.Port { get; set; }

        string IRabbitMqTransportSettings.Server { get; set; }

        string IRabbitMqTransportSettings.Username { get; set; }

        string IRabbitMqTransportSettings.VirtualHost { get; set; }

        IEndpointNameConvention IPipelineTransportSettings.EndpointNameConvention { get; set; }

        IPipelineNameConvention IPipelineTransportSettings.PipelineNameConvention { get; set; }

        public override void Build()
        {
            var builder = this as ISettingsBuilder;
            builder.Container.RegisterSingleton<IPipelineTransport>(() => new RabbitMqTransport(this as IRabbitMqTransportSettings), builder.Name);
        }
    }

    public static class RabbitMqTransportExtensions
    {
        public static T UseRabbitMqTransport<T>(
            this T self,
            Action<IRabbitMqTransportSettings> configure = null,
            Action<IPipelineTransportSettings> configureConventions = null)
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
            self.Username = ConnectionFactory.DefaultUser;
            self.Password = ConnectionFactory.DefaultPass;
            self.VirtualHost = ConnectionFactory.DefaultVHost;
            return self;
        }

        public static T WithEndpointPerBoundedContext<T>(this T self) where T : IPipelineTransportSettings
        {
            self.PipelineNameConvention = new RabbitMqPipelinePerApplication();
            self.EndpointNameConvention = new RabbitMqEndpointPerBoundedContext(self.PipelineNameConvention);
            return self;
        }
    }
}