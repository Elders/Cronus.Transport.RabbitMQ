using System.Linq;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;
using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory : ConnectionFactory
    {
        public RabbitMqConnectionFactory(IRabbitMqSettings settings)
        {
            HostName = settings.Server;
            Port = settings.Port;
            UserName = settings.Username;
            Password = settings.Password;
            VirtualHost = settings.VirtualHost;
            AutomaticRecoveryEnabled = false;

            CreateVirtualHostDefinedInSettings(settings);
        }

        void CreateVirtualHostDefinedInSettings(IRabbitMqSettings settings)
        {
            RabbitMqManagementClient managmentClient = new RabbitMqManagementClient(settings);
            if (!managmentClient.GetVHosts().Any(vh => vh.Name == settings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(settings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == settings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }
        }
    }
}
