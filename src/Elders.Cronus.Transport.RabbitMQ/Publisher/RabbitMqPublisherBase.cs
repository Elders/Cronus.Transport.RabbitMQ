using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class RabbitMqPublisherBase<TMessage> : Publisher<TMessage> where TMessage : IMessage
    {
        private readonly ISerializer serializer;
        private readonly PublisherChannelResolver channelResolver;
        private readonly IRabbitMqNamer rabbitMqNamer;
        private readonly ILogger logger;

        public RabbitMqPublisherBase(ISerializer serializer, PublisherChannelResolver channelResolver, IRabbitMqNamer rabbitMqNamer, IEnumerable<DelegatingPublishHandler> handlers, ILogger logger)
            : base(handlers)
        {
            this.serializer = serializer;
            this.channelResolver = channelResolver;
            this.rabbitMqNamer = rabbitMqNamer;
            this.logger = logger;
        }

        /// <summary>
        /// Publishes the supplied <paramref name="message"/> to all RabbitMQ exchanges resolved for it.
        /// </summary>
        /// <param name="message">The Cronus message to publish.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous publish.</param>
        protected override async Task<PublishResult> PublishInternalAsync(CronusMessage message, CancellationToken cancellationToken)
        {
            PublishResult publishResult = PublishResult.Initial;

            string boundedContext = message.BoundedContext;

            IEnumerable<string> exchanges = GetExistingExchangesNames(message);

            IEnumerable<IRabbitMqOptions> scopedOptions = GetOptionsFor(message);

            foreach (IRabbitMqOptions scopedOpt in scopedOptions)
            {
                foreach (string exchange in exchanges)
                {
                    publishResult &= await PublishAsync(message, boundedContext, exchange, scopedOpt, cancellationToken).ConfigureAwait(false);
                }
            }

            return publishResult;
        }

        protected abstract IEnumerable<IRabbitMqOptions> GetOptionsFor(CronusMessage message);

        private async Task<PublishResult> PublishAsync(CronusMessage message, string boundedContext, string exchange, IRabbitMqOptions options, CancellationToken cancellationToken)
        {
            try
            {
                IChannel exchangeChannel = await channelResolver.ResolveAsync(exchange, options, boundedContext, cancellationToken).ConfigureAwait(false);
                BasicProperties props = new BasicProperties();
                props = BuildMessageProperties(props, message);
                props = AttachHeaders(props, message);

                byte[] body = serializer.SerializeToBytes(message);
                await exchangeChannel.BasicPublishAsync(exchange, string.Empty, false, props, body, cancellationToken).ConfigureAwait(false);
                logger.LogDebug("Published message to exchange {exchange} with headers {@headers}.", exchange, props.Headers);

                return new PublishResult(true, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Published message to exchange {exchange} has FAILED.", exchange);

                return PublishResult.Failed;
            }
        }

        protected virtual BasicProperties BuildMessageProperties(BasicProperties properties, CronusMessage message)
        {
            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add("cronus_messageid", message.Id.ToByteArray());
            string ttl = message.GetTtlMilliseconds();
            if (string.IsNullOrEmpty(ttl) == false)
                properties.Expiration = ttl;
            properties.Persistent = true;

            return properties;
        }

        protected virtual BasicProperties AttachHeaders(BasicProperties properties, CronusMessage message)
        {
            string boundedContext = message.BoundedContext;

            properties.Headers ??= new Dictionary<string, object>();
            properties.Headers.Add(message.GetMessageType().GetContractId(), boundedContext);

            return properties;
        }

        private IEnumerable<string> GetExistingExchangesNames(CronusMessage message)
        {
            Type messageType = message.GetMessageType();

            IEnumerable<string> exchanges = rabbitMqNamer.Get_PublishTo_ExchangeNames(messageType);

            if (string.IsNullOrEmpty(message.GetTtlMilliseconds()) == false)
                exchanges = exchanges.Select(e => $"{e}.Delayer");

            return exchanges;
        }
    }
}
