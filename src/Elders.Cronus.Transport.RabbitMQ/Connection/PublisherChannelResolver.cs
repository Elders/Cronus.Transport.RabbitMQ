using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublisherChannelResolver : ChannelResolverBase // channels per exchange
    {
        public PublisherChannelResolver(ConnectionResolver connectionResolver) : base(connectionResolver) { }

        /// <summary>
        /// Resolves a publisher channel for the supplied <paramref name="exchange"/>, declaring the exchange if it does not yet exist.
        /// </summary>
        /// <param name="exchange">The exchange name to publish to.</param>
        /// <param name="options">The RabbitMQ options used when a new connection must be opened.</param>
        /// <param name="boundedContext">The bounded context name used to scope the underlying connection.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous resolve.</param>
        /// <returns>A task whose result is an open RabbitMQ channel ready to publish to the supplied exchange.</returns>
        public override async Task<IChannel> ResolveAsync(string exchange, IRabbitMqOptions options, string boundedContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(exchange);
            ArgumentNullException.ThrowIfNull(options);

            string channelKey = $"{boundedContext}_{options.GetType().Name}_{exchange}_{options.Server}".ToLower();
            string connectionKey = $"{options.VHost}_{options.Server}".ToLower();

            IChannel channel = GetExistingChannel(channelKey);

            if (channel is null || channel.IsClosed)
            {
                await @lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    channel = GetExistingChannel(channelKey);

                    if (channel?.IsClosed == true)
                    {
                        channels.Remove(channelKey);
                        channel = null;
                    }

                    if (channel is null)
                    {
                        IConnection connection = await connectionResolver.ResolveAsync(connectionKey, options, cancellationToken).ConfigureAwait(false);
                        IChannel scopedChannel = await CreateChannelForPublisherAsync(connection, cancellationToken).ConfigureAwait(false);
                        try
                        {
                            if (string.IsNullOrEmpty(exchange) == false)
                            {
                                await scopedChannel.ExchangeDeclarePassiveAsync(exchange, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (OperationInterruptedException)
                        {
                            await scopedChannel.DisposeAsync().ConfigureAwait(false);
                            scopedChannel = await CreateChannelForPublisherAsync(connection, cancellationToken).ConfigureAwait(false);
                            await scopedChannel.ExchangeDeclareAsync(exchange, PipelineType.Headers.ToString(), true, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }

                        channels.Add(channelKey, scopedChannel);
                    }
                }
                finally
                {
                    @lock.Release();
                }
            }

            return GetExistingChannel(channelKey);
        }

        private static Task<IChannel> CreateChannelForPublisherAsync(IConnection connection, CancellationToken cancellationToken)
        {
            // Publisher confirmations + tracking are hardcoded ON: trades ~250 µs of
            // latency per sequential publish for at-least-once delivery guarantees.
            // Workloads that don't need durability (telemetry, heartbeats, idempotent
            // retried events) currently have no opt-out — see docs/benchmarks/README.md
            // for the perf trade-offs and the v12.1 follow-up to expose these via
            // RabbitMqOptions.
            return connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
        }
    }
}
