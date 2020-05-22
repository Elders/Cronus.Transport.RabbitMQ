using System.Text.Json.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Management.Model
{
    //{
    // "component": "federation-upstream",
    // "vhost": "unicom-volume",
    // "name": "unicon-public-events",
    // "value": {
    //  "uri": "amqp://node1/unicom-public",
    //  "ack-mode": "on-confirm",
    //  "trust-user-id": false,
    //  "exchange": "PublicEvents",
    //  "max-hops": 2
    // }
    //}
    public class FederatedExchange
    {
        [JsonPropertyName("component")]
        public string Component { get; set; } = "federation-upstream";

        [JsonPropertyName("vhost")]
        public string Vhost { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public ValueParameters Value { get; set; }

        public class ValueParameters
        {
            [JsonPropertyName("uri")]
            public string Uri { get; set; }

            [JsonPropertyName("ack-mode")]
            public string AckMode { get; set; } = "on-confirm";

            [JsonPropertyName("trust-user-id")]
            public bool TrustUserId { get; set; } = false;

            [JsonPropertyName("exchange")]
            public string Exchange { get; set; }

            [JsonPropertyName("max-hops")]
            public int MaxHops { get; set; }
        }
    }
}
