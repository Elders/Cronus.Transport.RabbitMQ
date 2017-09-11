using System.Collections.Generic;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management;
using System.Linq;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management.Model;
using Elders.Cronus.Serializer;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqEndpointFactory : IEndpointFactory
    {
        private readonly ISerializer _serializer;
        private readonly IEndpointNameConvention _endpointNameConvention;
        private readonly RabbitMqSession _session;
        private readonly Config.IRabbitMqTransportSettings _settings;

        public RabbitMqEndpointFactory(ISerializer serializer, RabbitMqSession session, Config.IRabbitMqTransportSettings settings)
        {
            this._serializer = serializer;
            this._settings = settings;
            this._endpointNameConvention = settings.EndpointNameConvention;
            this._session = session;
        }

        public IEndpoint CreateEndpoint(EndpointDefinition definition)
        {
            var managmentClient = new RabbitMqManagementClient(_settings.Server, _settings.Username, _settings.Password, _settings.AdminPort);

            if (!managmentClient.GetVHosts().Any(vh => vh.Name == _settings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(_settings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == _settings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }

            var endpoint = new RabbitMqEndpoint(_serializer, definition, _session, _settings);
            endpoint.RoutingHeaders.Add("x-match", "any");
            endpoint.Declare();

            var pipeLine = new UberPipeline(this._serializer, definition.PipelineName, _session, PipelineType.Headers);
            pipeLine.Declare();
            pipeLine.Bind(endpoint);
            return endpoint;
        }

        public IEndpoint CreateTopicEndpoint(EndpointDefinition definition)
        {
            var managmentClient = new RabbitMqManagementClient(_settings.Server, _settings.Username, _settings.Password, _settings.AdminPort);

            if (!managmentClient.GetVHosts().Any(vh => vh.Name == _settings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(_settings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == _settings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }

            var endpoint = new RabbitMqEndpoint(_serializer, definition, _session, _settings);
            endpoint.Declare();

            var pipeLine = new RabbitMqPipeline(_serializer, definition.PipelineName, _session, PipelineType.Topics);
            pipeLine.Declare();
            pipeLine.Bind(endpoint);
            return endpoint;
        }

        public IEnumerable<EndpointDefinition> GetEndpointDefinition(IEndpointConsumer consumer, SubscriptionMiddleware subscriptionMiddleware)
        {
            return _endpointNameConvention.GetEndpointDefinition(consumer, subscriptionMiddleware);
        }
    }
}
