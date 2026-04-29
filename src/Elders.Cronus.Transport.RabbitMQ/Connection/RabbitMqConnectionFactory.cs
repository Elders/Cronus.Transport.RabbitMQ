using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory<TOptions> : IRabbitMqConnectionFactory where TOptions : IRabbitMqOptions
    {
        private readonly ILogger<RabbitMqConnectionFactory<TOptions>> logger;
        private readonly TOptions options;

        public RabbitMqConnectionFactory(IOptionsMonitor<TOptions> optionsMonitor, ILogger<RabbitMqConnectionFactory<TOptions>> logger)
        {
            options = optionsMonitor.CurrentValue;
            this.logger = logger;
        }

        /// <summary>
        /// Creates a new <see cref="IConnection"/> using the default options registered with this factory.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the connection establishment.</param>
        /// <returns>A task whose result is the established RabbitMQ connection.</returns>
        public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            return CreateConnectionWithOptionsAsync(options, cancellationToken);
        }

        /// <summary>
        /// Creates a new <see cref="IConnection"/> using the supplied <paramref name="options"/>.
        /// </summary>
        /// <param name="options">The RabbitMQ options to use for the connection.</param>
        /// <param name="cancellationToken">A token to cancel the connection establishment.</param>
        /// <returns>A task whose result is the established RabbitMQ connection.</returns>
        public async Task<IConnection> CreateConnectionWithOptionsAsync(IRabbitMqOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Loaded RabbitMQ options are {@Options}", options);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var connectionFactory = new ConnectionFactory();
                    connectionFactory.Port = options.Port;
                    connectionFactory.UserName = options.Username;
                    connectionFactory.Password = options.Password;
                    connectionFactory.VirtualHost = options.VHost;
                    connectionFactory.AutomaticRecoveryEnabled = true;
                    connectionFactory.Ssl.Enabled = options.UseSsl;
                    connectionFactory.EndpointResolverFactory = (_) => MultipleEndpointResolver.ComposeEndpointResolver(options);

                    return await connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is BrokerUnreachableException)
                        logger.LogWarning("Failed to create RabbitMQ connection using options {@options}. Retrying...", options);
                    else
                        logger.LogWarning(ex, "Failed to create RabbitMQ connection using options {@options}. Retrying...", options);

                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            MultipleEndpointResolver(AmqpTcpEndpoint[] amqpTcpEndpoints) : base(amqpTcpEndpoints) { }

            public static MultipleEndpointResolver ComposeEndpointResolver(IRabbitMqOptions options)
            {
                AmqpTcpEndpoint[] endpoints = AmqpTcpEndpoint.ParseMultiple($"{options.Server}:{options.Port}");

                if (options.UseSsl is false)
                    return new MultipleEndpointResolver(endpoints);

                foreach (AmqpTcpEndpoint endp in endpoints)
                {
                    endp.Ssl.Enabled = true;
                    endp.Ssl.ServerName = options.Server;
                }

                return new MultipleEndpointResolver(endpoints);
            }
        }
    }
}
