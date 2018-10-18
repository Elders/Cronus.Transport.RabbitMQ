using Microsoft.Extensions.Configuration;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Config
{
    public class RabbitMqSettings
    {
        public RabbitMqSettings(IConfiguration configuration)
        {
            Server = configuration["cronus_transport_rabbimq_server"];
            Port = configuration.GetValue<int>("cronus_transport_rabbimq_port", 5672);
            VirtualHost = configuration["cronus_transport_rabbimq_vhost"];
            Username = configuration["cronus_transport_rabbimq_username"];
            Password = configuration["cronus_transport_rabbimq_password"];
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
