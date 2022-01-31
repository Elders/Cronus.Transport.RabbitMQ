using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PrivateRabbitMqPublisher<TMessage> : TestPublisherBase<TMessage> where TMessage : IMessage
    {
        public PrivateRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<RabbitMqOptions> optionsMonitor, BoundedContextRabbitMqNamer rabbitMqNamer) : base(serializer, channelResolver, tenantResolver, boundedContext, optionsMonitor.CurrentValue, rabbitMqNamer)
        {
        }
    }

    public abstract class TestPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(TestPublisherBase<>));

        bool isStopped = false;

        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;

        private IRabbitMqOptions options;

        public TestPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IRabbitMqOptions options, IRabbitMqNamer rabbitMqNamer)
            : base(tenantResolver, boundedContext.CurrentValue)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.options = options;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        protected virtual IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.Headers[MessageHeader.BoundedContext];

            properties.Headers = new Dictionary<string, object>() { { message.Payload.GetType().GetContractId(), boundedContext } };
            properties.Persistent = true;

            return properties;
        }

        protected override bool PublishInternal(CronusMessage message)
        {
            try
            {
                if (isStopped)
                {
                    logger.Warn(() => "Failed to publish a message. Publisher is stopped/disposed.");
                    return false;
                }

                string boundedContext = message.Headers[MessageHeader.BoundedContext];
                IModel publishModel = channelResolver.Resolve($"{boundedContext}_{options.GetType().Name}", options);

                IBasicProperties props = publishModel.CreateBasicProperties();
                props = BuildMessageProperties(props, message);

                byte[] body = this.serializer.SerializeToBytes(message);

                var publishDelayInMiliseconds = message.GetPublishDelay();
                if (publishDelayInMiliseconds < 1000)
                {
                    foreach (var exchange in rabbitMqNamer.GetExchangeNames(message.Payload.GetType()))
                    {
                        publishModel.BasicPublish(exchange, string.Empty, false, props, body);
                    }
                }
                else
                {
                    foreach (var exchange in rabbitMqNamer.GetExchangeNames(message.Payload.GetType()))
                    {
                        var exchangeName = $"{exchange}.Scheduler";
                        props.Headers.Add("x-delay", message.GetPublishDelay());
                        publishModel.BasicPublish(exchangeName, string.Empty, false, props, body);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.WarnException(ex, () => ex.Message);
                //lock (connectionResolver)
                //{
                //    publishModel?.Abort();
                //    publishModel = null;
                //}
                return false;
            }
        }
    }
}
