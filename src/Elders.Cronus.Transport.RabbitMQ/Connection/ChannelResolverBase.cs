using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class ChannelResolverBase
    {
        protected readonly Dictionary<string, IChannel> channels;
        protected readonly ConnectionResolver connectionResolver;
        protected static readonly SemaphoreSlim @lock = new SemaphoreSlim(1, 1);

        public ChannelResolverBase(ConnectionResolver connectionResolver)
        {
            ArgumentNullException.ThrowIfNull(connectionResolver);

            channels = new Dictionary<string, IChannel>();
            this.connectionResolver = connectionResolver;
        }

        /// <summary>
        /// Resolves a cached open <see cref="IChannel"/> for the supplied <paramref name="resolveKey"/> or creates a new one if needed.
        /// </summary>
        /// <param name="resolveKey">The cache key (typically a queue or exchange identifier) used to deduplicate channels.</param>
        /// <param name="options">The RabbitMQ options used when a new connection must be opened.</param>
        /// <param name="boundedContext">The bounded context name used to scope the underlying connection.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous resolve.</param>
        /// <returns>A task whose result is an open RabbitMQ channel associated with the supplied key.</returns>
        public virtual async Task<IChannel> ResolveAsync(string resolveKey, IRabbitMqOptions options, string boundedContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resolveKey);
            ArgumentNullException.ThrowIfNull(options);

            resolveKey = resolveKey.ToLower();

            IChannel channel = GetExistingChannel(resolveKey);

            if (channel is null || channel.IsClosed)
            {
                await @lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    channel = GetExistingChannel(resolveKey);

                    if (channel?.IsClosed == true)
                    {
                        channels.Remove(resolveKey);
                        channel = null;
                    }

                    if (channel is null)
                    {
                        IConnection connection = await connectionResolver.ResolveAsync(boundedContext, options, cancellationToken).ConfigureAwait(false);
                        // Publisher confirmations + tracking are hardcoded ON: trades ~250 µs of
                        // latency per sequential publish for at-least-once delivery guarantees.
                        // Workloads that don't need durability (telemetry, heartbeats, idempotent
                        // retried events) currently have no opt-out — see docs/benchmarks/README.md
                        // for the perf trade-offs and the v12.1 follow-up to expose these via
                        // RabbitMqOptions.
                        IChannel scopedChannel = await connection.CreateChannelAsync(
                            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                            cancellationToken).ConfigureAwait(false);

                        channels.Add(resolveKey, scopedChannel);
                    }
                }
                finally
                {
                    @lock.Release();
                }
            }

            return GetExistingChannel(resolveKey);
        }

        protected IChannel GetExistingChannel(string resolveKey)
        {
            channels.TryGetValue(resolveKey, out IChannel channel);

            return channel;
        }
    }
}
