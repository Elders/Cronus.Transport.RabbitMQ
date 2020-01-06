using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory : ConnectionFactory
    {
        public RabbitMqConnectionFactory(RabbitMqSettings settings)
        {
            HostName = settings.Server;
            Port = settings.Port;
            UserName = settings.Username;
            Password = settings.Password;
            VirtualHost = settings.VirtualHost;
            AutomaticRecoveryEnabled = false;
            EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(settings); };

            CreateVirtualHostDefinedInSettings(settings);
        }

        void CreateVirtualHostDefinedInSettings(RabbitMqSettings settings)
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

        private class MultipleEndpointResolver : IEndpointResolver
        {
            RabbitMqSettings settings;

            public MultipleEndpointResolver(RabbitMqSettings settings)
            {
                this.settings = settings;
            }

            public IEnumerable<AmqpTcpEndpoint> All()
            {
                return AmqpTcpEndpoint.ParseMultiple(settings.Server);
            }
        }
    }
}
