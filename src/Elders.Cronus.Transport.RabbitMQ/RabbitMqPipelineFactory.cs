using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management.Model;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqPipelineFactory : IPipelineFactory<IRabbitMqPipeline>
    {
        private readonly IPipelineNameConvention nameConvention;

        private ConcurrentDictionary<string, IRabbitMqPipeline> pipes = new ConcurrentDictionary<string, IRabbitMqPipeline>();

        private RabbitMqSession session;

        private Config.IRabbitMqTransportSettings transportSettings;

        public RabbitMqPipelineFactory(RabbitMqSession session, Config.IRabbitMqTransportSettings settings)
        {
            this.transportSettings = settings;
            this.nameConvention = settings.PipelineNameConvention;
            this.session = session;
        }

        public IRabbitMqPipeline GetPipeline(string pipelineName)
        {
            if (!pipes.ContainsKey(pipelineName))
            {
                var managmentClient = new RabbitMqManagementClient(transportSettings.Server, transportSettings.Username, transportSettings.Password, transportSettings.AdminPort);

                if (!managmentClient.GetVHosts().Any(vh => vh.Name == transportSettings.VirtualHost))
                {
                    var vhost = managmentClient.CreateVirtualHost(transportSettings.VirtualHost);
                    var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == transportSettings.Username);
                    var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                    managmentClient.CreatePermission(permissionInfo);
                }

                IRabbitMqPipeline pipeline = new UberPipeline(pipelineName, session, PipelineType.Headers);
                pipeline.Open();
                pipes.TryAdd(pipelineName, pipeline);
                return pipeline;
            }
            return pipes[pipelineName];
        }

        public IRabbitMqPipeline GetPipeline(Type messageType)
        {
            string pipelineName = nameConvention.GetPipelineName(messageType);
            return GetPipeline(pipelineName);
        }
    }
}
