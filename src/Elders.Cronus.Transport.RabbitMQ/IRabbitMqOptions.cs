using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqOptions
    {
        int AdminPort { get; set; }
        string Password { get; set; }
        int Port { get; set; }
        string Server { get; set; }
        string Username { get; set; }
        string VHost { get; set; }
        bool UseSsl { get; set; }
        string ApiAddress { get; set; }
        public string BoundedContext { get; set; }
        FederatedExchangeOptions FederatedExchange { get; set; }
        IEnumerable<IRabbitMqOptions> GetOptionsFor(string boundedContext);
    }
}
