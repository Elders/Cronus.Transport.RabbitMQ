using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConnectionResolver : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, IConnection> connectionsPerVHost;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1, 1);

        public ConnectionResolver(IRabbitMqConnectionFactory connectionFactory)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);

            connectionsPerVHost = new ConcurrentDictionary<string, IConnection>();
            this.connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Resolves an existing open <see cref="IConnection"/> for the given <paramref name="key"/> or creates a new one using <paramref name="options"/>.
        /// </summary>
        /// <param name="key">A logical key (typically a vhost+server combination) used to deduplicate connections.</param>
        /// <param name="options">The RabbitMQ options to use when a new connection must be created.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous resolve.</param>
        /// <returns>A task whose result is an open RabbitMQ connection associated with the supplied key.</returns>
        public async Task<IConnection> ResolveAsync(string key, IRabbitMqOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(options);

            IConnection connection = GetExistingConnection(key);

            if (connection is null || connection.IsOpen == false)
            {
                await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    connection = GetExistingConnection(key);
                    if (connection is null || connection.IsOpen == false)
                    {
                        connection = await CreateConnectionAsync(key, options, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    connectionLock.Release();
                }
            }

            return connection;
        }

        private IConnection GetExistingConnection(string key)
        {
            connectionsPerVHost.TryGetValue(key, out IConnection connection);

            return connection;
        }

        private async Task<IConnection> CreateConnectionAsync(string key, IRabbitMqOptions options, CancellationToken cancellationToken)
        {
            IConnection connection = await connectionFactory.CreateConnectionWithOptionsAsync(options, cancellationToken).ConfigureAwait(false);

            if (connectionsPerVHost.TryGetValue(key, out _))
            {
                if (connectionsPerVHost.TryRemove(key, out _))
                    connectionsPerVHost.TryAdd(key, connection);
            }
            else
            {
                connectionsPerVHost.TryAdd(key, connection);
            }

            return connection;
        }

        /// <summary>
        /// Closes all tracked RabbitMQ connections.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            foreach (var connection in connectionsPerVHost)
            {
                try
                {
                    await connection.Value.CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort close on shutdown
                }
            }

            connectionLock.Dispose();
        }
    }
}
