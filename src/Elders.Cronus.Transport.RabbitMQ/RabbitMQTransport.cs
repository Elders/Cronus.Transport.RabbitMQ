using System;
using System.Collections.Concurrent;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqTransport : IPipelineTransport, ITransport, IDisposable
    {
        static ConcurrentDictionary<string, RabbitMqSession> sessions = new ConcurrentDictionary<string, RabbitMqSession>();

        private string connectionString;

        public RabbitMqTransport(string server, int port, int restApiPort, string username, string password, string virtualHost, IPipelineNameConvention pipelineNameConvention, IEndpointNameConvention endpointNameConvention)
        {
            connectionString = server + port + username + password + virtualHost;
            var session = sessions.GetOrAdd(connectionString, x =>
             {
                 var rabbitSessionFactory = new RabbitMqSessionFactory(server, port, restApiPort, username, password, virtualHost);
                 return rabbitSessionFactory.OpenSession();
             });


            PipelineFactory = new RabbitMqPipelineFactory(session, pipelineNameConvention);
            EndpointFactory = new RabbitMqEndpointFactory(session, endpointNameConvention);
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
