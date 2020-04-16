using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConnectionFactory : ConnectionFactory
    {
        public RabbitMqConnectionFactory(IOptionsMonitor<RabbitMqOptions> settings)
        {
            HostName = settings.CurrentValue.Server;
            Port = settings.CurrentValue.Port;
            UserName = settings.CurrentValue.Username;
            Password = settings.CurrentValue.Password;
            VirtualHost = settings.CurrentValue.VHost;
            AutomaticRecoveryEnabled = false;
            EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(settings); };

            CreateVirtualHostDefinedInSettings(settings.CurrentValue);
        }

        void CreateVirtualHostDefinedInSettings(RabbitMqOptions settings)
        {
            RabbitMqManagementClient managmentClient = new RabbitMqManagementClient(settings);
            if (!managmentClient.GetVHosts().Any(vh => vh.Name == settings.VHost))
            {
                var vhost = managmentClient.CreateVirtualHost(settings.VHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == settings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }
        }

        private class MultipleEndpointResolver : IEndpointResolver
        {
            RabbitMqOptions settings;

            public MultipleEndpointResolver(IOptionsMonitor<RabbitMqOptions> settings)
            {
                this.settings = settings.CurrentValue;
            }

            public IEnumerable<AmqpTcpEndpoint> All()
            {
                return AmqpTcpEndpoint.ParseMultiple(settings.Server);
            }
        }
    }
}
