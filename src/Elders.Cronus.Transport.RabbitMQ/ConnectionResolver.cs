using System;
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConnectionResolver
    {
        private readonly ConcurrentDictionary<string, IConnection> connectionsPerVHost;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private static readonly object connectionLock = new object();

        public ConnectionResolver(IRabbitMqConnectionFactory connectionFactory)
        {
            connectionsPerVHost = new ConcurrentDictionary<string, IConnection>();
            this.connectionFactory = connectionFactory;
        }

        public IConnection Resolve(string boundedContext, IRabbitMqOptions options)
        {
            IConnection connection = GetExistingConnection(boundedContext);

            if (connection is null)
            {
                lock (connectionLock)
                {
                    connection = GetExistingConnection(boundedContext);

                    if (connection is null)
                    {
                        connection = CreateConnection(boundedContext, options);
                    }
                }
            }

            return connection;
        }

        private IConnection GetExistingConnection(string boundedContext)
        {
            connectionsPerVHost.TryGetValue(boundedContext, out IConnection connection);

            return connection;
        }

        private IConnection CreateConnection(string boundedContext, IRabbitMqOptions options)
        {
            IConnection connection = connectionFactory.CreateConnectionWithOptions(options);
            if (connectionsPerVHost.TryAdd(boundedContext, connection) == false)
                throw new Exception("Kak go napravi tova?");

            return connection;
        }
    }
}
