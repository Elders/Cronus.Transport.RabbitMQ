using RabbitMQ.Client;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public sealed class RabbitMqSessionFactory
    {
        ConnectionFactory factory;

        readonly string hostname;

        readonly string password;

        readonly int port;

        readonly string username;

        readonly string virtualHost;

        public RabbitMqSessionFactory(string hostname = "localhost", int port = 5672, string username = ConnectionFactory.DefaultUser, string password = ConnectionFactory.DefaultPass, string virtualHost = ConnectionFactory.DefaultVHost)
        {
            this.hostname = hostname;
            this.username = username;
            this.password = password;
            this.port = port;
            this.virtualHost = virtualHost;
            factory = new ConnectionFactory
            {
                HostName = hostname,
                Port = port,
                UserName = username,
                Password = password,
                VirtualHost = virtualHost,
                AutomaticRecoveryEnabled = true
            };
        }

        public RabbitMqSession OpenSession()
        {
            return new RabbitMqSession(factory);
        }
    }
}