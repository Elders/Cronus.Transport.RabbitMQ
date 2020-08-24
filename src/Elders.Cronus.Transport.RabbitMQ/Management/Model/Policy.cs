using System.Text.Json.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Management.Model
{
    //{
    // "vhost": "unicom-virtualinventory",
    // "name": "vi-public-events",
    // "pattern": "virtualinventory.Events$",
    // "apply-to": "exchanges",
    // "definition": {
    //  "federation-upstream": "unicom-public-public-events"
    // }
    //}
    public class Policy
    {
        [JsonPropertyName("vhost")]
        public string VHost { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }

        [JsonPropertyName("apply-to")]
        public string ApplyTo { get; set; } = "exchanges";

        [JsonPropertyName("definition")]
        public DefinitionDto Definition { get; set; }

        public class DefinitionDto
        {
            [JsonPropertyName("federation-upstream")]
            public string FederationUpstream { get; set; }
        }
    }
}
