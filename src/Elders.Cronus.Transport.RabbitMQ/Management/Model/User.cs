using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Management.Model
{
    public class User
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("password_hash")]
        public string PasswordHash { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }
}
