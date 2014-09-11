using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqTransport : IPipelineTransport, ITransport, IDisposable
    {
        static ConcurrentDictionary<string, RabbitMqSession> sessions = new ConcurrentDictionary<string, RabbitMqSession>();

        private string connectionString;

        public RabbitMqTransport(Elders.Cronus.Pipeline.Transport.RabbitMQ.Config.IRabbitMqTransportSettings settings)
        {
            connectionString = settings.Server + settings.Port + settings.Username + settings.Password + settings.VirtualHost;
            var session = sessions.GetOrAdd(connectionString, x =>
             {
                 var rabbitSessionFactory = new RabbitMqSessionFactory(settings.Server, settings.Port, settings.Username, settings.Password, settings.VirtualHost);
                 return rabbitSessionFactory.OpenSession();
             });


            PipelineFactory = new RabbitMqPipelineFactory(session, settings.PipelineNameConvention.Value);
            EndpointFactory = new RabbitMqEndpointFactory(session, settings.EndpointNameConvention.Value);
        }
        public IEndpointFactory EndpointFactory { get; private set; }

        public IPipelineFactory<IPipeline> PipelineFactory { get; private set; }

        public void Dispose()
        {
            RabbitMqSession session;
            if (sessions.TryRemove(connectionString, out session))
            {
                session.Dispose();
            }
        }
    }
}
