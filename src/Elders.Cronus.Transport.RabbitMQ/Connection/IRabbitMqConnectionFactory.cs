using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionFactory
    {
        /// <summary>
        /// Creates a new <see cref="IConnection"/> using the default options registered with this factory.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the connection establishment.</param>
        /// <returns>A task whose result is the established RabbitMQ connection.</returns>
        Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new <see cref="IConnection"/> using the supplied <paramref name="options"/>.
        /// </summary>
        /// <param name="options">The RabbitMQ options to use for the connection.</param>
        /// <param name="cancellationToken">A token to cancel the connection establishment.</param>
        /// <returns>A task whose result is the established RabbitMQ connection.</returns>
        Task<IConnection> CreateConnectionWithOptionsAsync(IRabbitMqOptions options, CancellationToken cancellationToken = default);
    }
}
