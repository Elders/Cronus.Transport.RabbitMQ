using Newtonsoft.Json.Serialization;
using System.Text.RegularExpressions;

namespace Elders.Cronus.Transport.RabbitMQ.Management
{
    public class RabbitContractResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName)
        {
            return Regex.Replace(propertyName, "([a-z])([A-Z])", "$1_$2").ToLower();
        }
    }
}
