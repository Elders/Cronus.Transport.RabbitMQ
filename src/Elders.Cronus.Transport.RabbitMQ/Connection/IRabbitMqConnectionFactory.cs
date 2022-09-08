using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionFactory
    {
        IConnection CreateConnection(string connectionName);
        IConnection CreateConnectionWithOptions(string connectionName, IRabbitMqOptions options);
    }
}
