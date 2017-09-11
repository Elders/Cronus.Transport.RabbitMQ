using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management.Model;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Elders.Cronus.Serializer;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqPipelineFactory : IPipelineFactory<IRabbitMqPipeline>
    {
        private readonly IPipelineNameConvention _nameConvention;
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<string, IRabbitMqPipeline> _pipes = new ConcurrentDictionary<string, IRabbitMqPipeline>();
        private readonly RabbitMqSession _session;
        private readonly Config.IRabbitMqTransportSettings _settings;

        public RabbitMqPipelineFactory(ISerializer serializer, RabbitMqSession session, Config.IRabbitMqTransportSettings settings)
        {
            this._serializer = serializer;
            this._settings = settings;
            this._nameConvention = settings.PipelineNameConvention;
            this._session = session;
        }

        public IRabbitMqPipeline GetPipeline(string pipelineName)
        {
            if (!_pipes.ContainsKey(pipelineName))
            {
                var managmentClient = new RabbitMqManagementClient(_settings.Server, _settings.Username, _settings.Password, _settings.AdminPort);

                if (!managmentClient.GetVHosts().Any(vh => vh.Name == _settings.VirtualHost))
                {
                    var vhost = managmentClient.CreateVirtualHost(_settings.VirtualHost);
                    var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == _settings.Username);
                    var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                    managmentClient.CreatePermission(permissionInfo);
                }

                IRabbitMqPipeline pipeline = new UberPipeline(this._serializer, pipelineName, _session, PipelineType.Headers);
                pipeline.Open();
                _pipes.TryAdd(pipelineName, pipeline);
                return pipeline;
            }
            return _pipes[pipelineName];
        }

        public IRabbitMqPipeline GetPipeline(Type messageType)
        {
            string pipelineName = _nameConvention.GetPipelineName(messageType);
            return GetPipeline(pipelineName);
        }
    }
}
