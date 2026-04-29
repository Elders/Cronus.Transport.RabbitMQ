using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Publisher
{
    public sealed class SignalRabbitMqPublisher : PublisherBase<ISignal>
    {
        private readonly PublisherChannelResolver channelResolver;
        private readonly BoundedContext boundedContext;
        private readonly RabbitMqOptions internalOptions;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ISerializer serializer;
        private readonly ILogger<SignalRabbitMqPublisher> logger;
        private readonly PublicRabbitMqOptionsCollection options;

        public SignalRabbitMqPublisher(IOptionsMonitor<BoundedContext> boundedContextOptionsMonitor, IOptionsMonitor<RabbitMqOptions> internalOptionsMonitor, IOptionsMonitor<PublicRabbitMqOptionsCollection> optionsMonitor, IRabbitMqNamer rabbitMqNamer, PublisherChannelResolver channelResolver, ISerializer serializer, ILogger<SignalRabbitMqPublisher> logger, IEnumerable<DelegatingPublishHandler> handlers)
            : base(handlers)
        {
            this.boundedContext = boundedContextOptionsMonitor.CurrentValue;
            this.internalOptions = internalOptionsMonitor.CurrentValue;
            this.rabbitMqNamer = rabbitMqNamer;
            options = optionsMonitor.CurrentValue;
            this.channelResolver = channelResolver;
            this.serializer = serializer;
            this.logger = logger;
        }

        /// <summary>
        /// Publishes the supplied signal <paramref name="message"/> to the appropriate internal or public exchanges.
        /// </summary>
        /// <param name="message">The Cronus signal message to publish.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous publish.</param>
        protected override async Task<PublishResult> PublishInternalAsync(CronusMessage message, CancellationToken cancellationToken)
        {
            PublishResult publishResult = PublishResult.Initial;

            Type messageType = null;

            try
            {
                string messageBC = message.BoundedContext;
                messageType = message.GetMessageType();
                IEnumerable<string> exchanges = rabbitMqNamer.Get_PublishTo_ExchangeNames(messageType);
                bool isInternalSignal = boundedContext.Name.Equals(messageBC, StringComparison.OrdinalIgnoreCase); // if the message will be published internally to the same BC

                if (isInternalSignal)
                {
                    foreach (var exchange in exchanges)
                    {
                        publishResult &= await PublishInternallyAsync(message, messageBC, exchange, internalOptions, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    foreach (var exchange in exchanges)
                    {
                        publishResult &= await PublishPublicallyAsync(message, messageBC, exchange, options.PublicClustersOptions, cancellationToken).ConfigureAwait(false);
                    }
                }

                return publishResult;
            }
            catch (Exception ex) when (True(() => logger.LogError(ex, "Unable to publish {cronus_messageType}", messageType)))
            {
                return PublishResult.Failed;
            }
        }

        private async Task<PublishResult> PublishInternallyAsync(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions internalOptions, CancellationToken cancellationToken)
        {
            IChannel exchangeChannel = await channelResolver.ResolveAsync(exchange, internalOptions, boundedContext, cancellationToken).ConfigureAwait(false);

            BasicProperties props = new BasicProperties();
            props = BuildMessageProperties(props, message);
            props = BuildInternalHeaders(props, message);

            return await PublishUsingChannelAsync(message, exchange, exchangeChannel, props, cancellationToken).ConfigureAwait(false);
        }

        private async Task<PublishResult> PublishPublicallyAsync(CronusMessage message, string boundedContext, string exchange, IEnumerable<IRabbitMqOptions> scopedOptions, CancellationToken cancellationToken)
        {
            PublishResult publishResult = PublishResult.Initial;

            foreach (var opt in scopedOptions)
            {
                IChannel exchangeChannel = await channelResolver.ResolveAsync(exchange, opt, boundedContext, cancellationToken).ConfigureAwait(false);

                BasicProperties props = new BasicProperties();
                props = BuildMessageProperties(props, message);
                props = BuildPublicHeaders(props, message);

                publishResult &= await PublishUsingChannelAsync(message, exchange, exchangeChannel, props, cancellationToken).ConfigureAwait(false);
            }

            return publishResult;
        }

        private async Task<PublishResult> PublishUsingChannelAsync(CronusMessage message, string exchange, IChannel exchangeChannel, BasicProperties properties, CancellationToken cancellationToken)
        {
            try
            {
                byte[] body = serializer.SerializeToBytes(message);
                await exchangeChannel.BasicPublishAsync(exchange, string.Empty, false, properties, body, cancellationToken).ConfigureAwait(false);

                logger.LogDebug("Published message to exchange {exchange} with headers {@headers}.", exchange, properties.Headers);

                return new PublishResult(true, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Published message to exchange {exchange} has FAILED.", exchange);

                return PublishResult.Failed;
            }
        }

        private BasicProperties BuildMessageProperties(BasicProperties properties, CronusMessage message)
        {
            string ttl = message.GetTtlMilliseconds();
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;
            properties.Persistent = false;
            properties.DeliveryMode = DeliveryModes.Transient;

            return properties;
        }

        private BasicProperties BuildPublicHeaders(BasicProperties properties, CronusMessage message)
        {
            string contractId = message.GetMessageType().GetContractId();
            string boundedContext = message.BoundedContext;
            string tenant = message.GetTenant();

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add($"{contractId}@{tenant}", boundedContext); // tenant specific binding in public signal
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());

            return properties;
        }

        private BasicProperties BuildInternalHeaders(BasicProperties properties, CronusMessage message)
        {
            string contractId = message.GetMessageType().GetContractId();
            string boundedContext = message.BoundedContext;

            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add($"{contractId}", boundedContext); // dont use tenant in internal signal
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());

            return properties;
        }
    }
}
