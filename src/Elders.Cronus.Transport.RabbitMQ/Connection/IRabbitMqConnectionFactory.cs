using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionFactory
    {
        IConnection CreateConnection();
        IConnection CreateConnectionWithOptions(IRabbitMqOptions options);
    }
}
