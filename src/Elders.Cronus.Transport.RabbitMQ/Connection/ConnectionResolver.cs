using System;
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConnectionResolver : IDisposable
    {
        private readonly ConcurrentDictionary<string, IConnection> connectionsPerVHost;
        private readonly IRabbitMqConnectionFactory connectionFactory;
        private static readonly object connectionLock = new object();

        public ConnectionResolver(IRabbitMqConnectionFactory connectionFactory)
        {
            connectionsPerVHost = new ConcurrentDictionary<string, IConnection>();
            this.connectionFactory = connectionFactory;
        }

        public IConnection Resolve(string name, IRabbitMqOptions options)
        {
            IConnection connection = GetExistingConnection(name);

            if (connection is null || connection.IsOpen == false)
            {
                lock (connectionLock)
                {
                    connection = GetExistingConnection(name);
                    if (connection is null || connection.IsOpen == false)
                    {
                        connection = CreateConnection(name, options);
                    }
                }
            }

            return connection;
        }

        public void CloseConnection(string name, string reason = "Connection was mannualy closed.")
        {
            IConnection connection = GetExistingConnection(name);
            connection.Close(1, reason, TimeSpan.FromSeconds(5));
        }

        private IConnection GetExistingConnection(string key)
        {
            connectionsPerVHost.TryGetValue(key, out IConnection connection);

            return connection;
        }

        private IConnection CreateConnection(string name, IRabbitMqOptions options)
        {
            IConnection connection = connectionFactory.CreateConnectionWithOptions(name, options);
            connectionsPerVHost.TryAdd(name, connection);

            return connection;
        }

        public void Dispose()
        {
            foreach (var connection in connectionsPerVHost)
            {
                connection.Value.Close(TimeSpan.FromSeconds(5));
            }
        }
    }
}
