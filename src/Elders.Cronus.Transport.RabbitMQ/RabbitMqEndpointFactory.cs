using System.Collections.Generic;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management;
using System.Linq;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management.Model;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqEndpointFactory : IEndpointFactory
    {
        private readonly IEndpointNameConvention endpointNameConvention;
        private readonly RabbitMqSession session;
        private Config.IRabbitMqTransportSettings transportSettings;

        public RabbitMqEndpointFactory(RabbitMqSession session, Config.IRabbitMqTransportSettings settings)
        {
            this.transportSettings = settings;
            this.endpointNameConvention = settings.EndpointNameConvention;
            this.session = session;
        }

        public IEndpoint CreateEndpoint(EndpointDefinition definition)
        {
            var managmentClient = new RabbitMqManagementClient(transportSettings.Server, transportSettings.Username, transportSettings.Password, transportSettings.Port);

            if (!managmentClient.GetVHosts().Any(vh => vh.Name == transportSettings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(transportSettings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == transportSettings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }

            var endpoint = new RabbitMqEndpoint(definition, session);
            endpoint.RoutingHeaders.Add("x-match", "any");
            endpoint.Declare();

            var pipeLine = new UberPipeline(definition.PipelineName, session, PipelineType.Headers);
            pipeLine.Declare();
            pipeLine.Bind(endpoint);
            return endpoint;
        }

        public IEndpoint CreateTopicEndpoint(EndpointDefinition definition)
        {
            var managmentClient = new RabbitMqManagementClient(transportSettings.Server, transportSettings.Username, transportSettings.Password, transportSettings.Port);

            if (!managmentClient.GetVHosts().Any(vh => vh.Name == transportSettings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(transportSettings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == transportSettings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }

            var endpoint = new RabbitMqEndpoint(definition, session);
            endpoint.Declare();

            var pipeLine = new RabbitMqPipeline(definition.PipelineName, session, PipelineType.Topics);
            pipeLine.Declare();
            pipeLine.Bind(endpoint);
            return endpoint;
        }

        public IEnumerable<EndpointDefinition> GetEndpointDefinition(IEndpointConsumer consumer, SubscriptionMiddleware subscriptionMiddleware)
        {
            return endpointNameConvention.GetEndpointDefinition(consumer, subscriptionMiddleware);
        }
    }
}
