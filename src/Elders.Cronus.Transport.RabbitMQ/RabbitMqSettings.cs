using Microsoft.Extensions.Configuration;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqSettings
    {
        public RabbitMqSettings(IConfiguration configuration)
        {
            Server = configuration.GetValue("cronus_transport_rabbimq_server", "127.0.0.1");
            Port = configuration.GetValue<int>("cronus_transport_rabbimq_port", 5672);
            VirtualHost = configuration.GetValue("cronus_transport_rabbimq_vhost", "/");
            Username = configuration.GetValue("cronus_transport_rabbimq_username", "guest");
            Password = configuration.GetValue("cronus_transport_rabbimq_password", "guest");
            AdminPort = configuration.GetValue<int>("cronus_transport_rabbimq_adminport");
        }

        public string Password { get; set; }

        public int Port { get; set; }

        public int AdminPort { get; set; }

        public string Server { get; set; }

        public string Username { get; set; }

        public string VirtualHost { get; set; }

    }
}
