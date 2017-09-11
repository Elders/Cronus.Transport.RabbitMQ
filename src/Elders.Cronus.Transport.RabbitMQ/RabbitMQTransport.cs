using System;
using System.Collections.Concurrent;
using Elders.Cronus.Serializer;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqTransport : IPipelineTransport, ITransport, IDisposable
    {
        static ConcurrentDictionary<string, RabbitMqSession> sessions = new ConcurrentDictionary<string, RabbitMqSession>();

        string connectionString;

        public RabbitMqTransport(ISerializer serializer, Elders.Cronus.Pipeline.Transport.RabbitMQ.Config.IRabbitMqTransportSettings settings)
        {
            connectionString = settings.Server + settings.Port + settings.Username + settings.Password + settings.VirtualHost;
            var session = sessions.GetOrAdd(connectionString, x =>
             {
                 var rabbitSessionFactory = new RabbitMqSessionFactory(settings.Server, settings.Port, settings.Username, settings.Password, settings.VirtualHost);
                 return rabbitSessionFactory.OpenSession();
             });


            PipelineFactory = new RabbitMqPipelineFactory(serializer, session, settings);
            EndpointFactory = new RabbitMqEndpointFactory(serializer, session, settings);
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
