using System.Text.Json.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Management.Model
{
    public class PermissionInfo
    {
        [JsonPropertyName("configure")]
        public string Configure { get; private set; }

        [JsonPropertyName("write")]
        public string Write { get; private set; }

        [JsonPropertyName("read")]
        public string Read { get; private set; }

        private readonly User user;
        private readonly Vhost vhost;

        private const string denyAll = "^$";
        private const string allowAll = ".*";

        public PermissionInfo(User user, Vhost vhost)
        {
            this.user = user;
            this.vhost = vhost;

            Configure = Write = Read = allowAll;
        }

        public string GetUserName()
        {
            return user.Name;
        }

        public string GetVirtualHostName()
        {
            return vhost.Name;
        }

        public PermissionInfo SetConfigure(string resourcesToAllow)
        {
            Configure = resourcesToAllow;
            return this;
        }

        public PermissionInfo SetWrite(string resourcedToAllow)
        {
            Write = resourcedToAllow;
            return this;
        }

        public PermissionInfo SetRead(string resourcesToAllow)
        {
            Read = resourcesToAllow;
            return this;
        }

        public PermissionInfo DenyAllConfigure()
        {
            Configure = denyAll;
            return this;
        }

        public PermissionInfo DenyAllWrite()
        {
            Write = denyAll;
            return this;
        }

        public PermissionInfo DenyAllRead()
        {
            Read = denyAll;
            return this;
        }
    }
}
