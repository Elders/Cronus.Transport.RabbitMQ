using System;
using Elders.Cronus.IocContainer;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.Serializer;
using Elders.Cronus.Transport.RabbitMQ;
using RabbitMQ.Client;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Config
{
    public interface IPipelineTransportSettings : ISettingsBuilder
    {
        //    IEndpointNameConvention EndpointNameConvention { get; set; }
        //    IPipelineNameConvention PipelineNameConvention { get; set; }
    }

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

        //IEndpointNameConvention IPipelineTransportSettings.EndpointNameConvention { get; set; }

        //IPipelineNameConvention IPipelineTransportSettings.PipelineNameConvention { get; set; }

        public override void Build()
        {
            var builder = this as ISettingsBuilder;
            builder.Container.RegisterSingleton<ITransport>(() =>
            {
                var serializer = builder.Container.Resolve<ISerializer>();
                return new RabbitMqTransport(this as IRabbitMqTransportSettings);
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

    public static class PipelineTransportSettingsExtensions
    {
        public static T WithEndpointPerBoundedContext<T>(this T self) where T : IPipelineTransportSettings
        {
            // self.PipelineNameConvention = new PipelinePerApplicationNameConvention();
            //self.EndpointNameConvention = new EndpointPerConsumerNameConvention(self.PipelineNameConvention);
            return self;
        }

    }

    public static class CronusConfigurationExtensions
    {
        public static T UsePipelineEventPublisher<T>(this T self, Action<EventPipelinePublisherSettings> configure = null) where T : IConsumerSettings
        {
            var builder = self as ISettingsBuilder;
            EventPipelinePublisherSettings settings = new EventPipelinePublisherSettings(self, builder.Name);
            if (configure != null)
                configure(settings);
            (settings as ISettingsBuilder).Build();
            return self;
        }

        public static T UsePipelineCommandPublisher<T>(this T self, Action<CommandPipelinePublisherSettings> configure = null) where T : IConsumerSettings
        {
            var builder = self as ISettingsBuilder;
            CommandPipelinePublisherSettings settings = new CommandPipelinePublisherSettings(self, builder.Name);
            if (configure != null)
                configure(settings);
            (settings as ISettingsBuilder).Build();
            return self;
        }

        public static T UsePipelineSagaPublisher<T>(this T self, Action<SagaPipelinePublisherSettings> configure = null) where T : IConsumerSettings
        {
            var builder = self as ISettingsBuilder;
            SagaPipelinePublisherSettings settings = new SagaPipelinePublisherSettings(self, builder.Name);
            if (configure != null)
                configure(settings);
            (settings as ISettingsBuilder).Build();
            return self;
        }

        public static T WithDefaultPublishers<T>(this T self) where T : IConsumerSettings
        {
            self
                .UsePipelineEventPublisher()
                .UsePipelineCommandPublisher()
                .UsePipelineSagaPublisher();
            return self;
        }
    }

    public abstract class PipelinePublisherSettings<TContract> : SettingsBuilder where TContract : IMessage
    {
        public PipelinePublisherSettings(ISettingsBuilder settingsBuilder, string name) : base(settingsBuilder, name) { }

        public override void Build()
        {
            var builder = this as ISettingsBuilder;
            Func<ITransport> transport = () => builder.Container.Resolve<ITransport>(builder.Name);
            Func<ISerializer> serializer = () => builder.Container.Resolve<ISerializer>();
            builder.Container.RegisterSingleton<IPublisher<TContract>>(() => transport().GetPublisher<TContract>(serializer()), builder.Name);
        }
    }

    public class CommandPipelinePublisherSettings : PipelinePublisherSettings<ICommand>
    {
        public CommandPipelinePublisherSettings(ISettingsBuilder settingsBuilder, string name) : base(settingsBuilder, name) { }
    }

    public class EventPipelinePublisherSettings : PipelinePublisherSettings<IEvent>
    {
        public EventPipelinePublisherSettings(ISettingsBuilder settingsBuilder, string name) : base(settingsBuilder, name) { }
    }

    public class SagaPipelinePublisherSettings : PipelinePublisherSettings<IScheduledMessage>
    {
        public SagaPipelinePublisherSettings(ISettingsBuilder settingsBuilder, string name) : base(settingsBuilder, name) { }
    }

}
