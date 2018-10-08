using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;
using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqTransport : ITransport, IDisposable
    {
        private IConnectionFactory connectionFactory;

        static ConcurrentDictionary<string, ConnectionFactory> connectionFactories = new ConcurrentDictionary<string, ConnectionFactory>();

        public RabbitMqTransport(IRabbitMqTransportSettings settings)
        {
            var connectionString = settings.Server + settings.Port + settings.Username + settings.Password + settings.VirtualHost;

            CreateVirtualHostDefinedInSettings(settings);

            connectionFactory = connectionFactories.GetOrAdd(connectionString, x => new ConnectionFactory
            {
                HostName = settings.Server,
                Port = settings.Port,
                UserName = settings.Username,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                AutomaticRecoveryEnabled = false
            });
        }

        void CreateVirtualHostDefinedInSettings(IRabbitMqTransportSettings settings)
        {
            RabbitMqManagementClient managmentClient = new RabbitMqManagementClient(settings);
            if (!managmentClient.GetVHosts().Any(vh => vh.Name == settings.VirtualHost))
            {
                var vhost = managmentClient.CreateVirtualHost(settings.VirtualHost);
                var rabbitMqUser = managmentClient.GetUsers().SingleOrDefault(x => x.Name == settings.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                managmentClient.CreatePermission(permissionInfo);
            }
        }

        public IPublisher<TMessage> GetPublisher<TMessage>(ISerializer serializer) where TMessage : IMessage
        {
            return new RabbitMqPublisher<TMessage>(serializer, connectionFactory);
        }

        public IEnumerable<IConsumerFactory> GetAvailableConsumers(ISerializer serializer, SubscriptionMiddleware subscriptions, string consumerName)
        {
            var messageType = subscriptions.Subscribers.FirstOrDefault().GetInvolvedMessageTypes().FirstOrDefault();
            var name = RabbitMqNamer.GetBoundedContext(messageType).ProductNamespace + "." + consumerName;
            //foreach (var item in subscriptions.Subscribers)
            {
                yield return new RabbitMqContiniousConsumerFactory(name, serializer, connectionFactory, subscriptions);
            }
        }

        public void Dispose()
        {
            connectionFactories?.Clear();
            connectionFactories = null;
            connectionFactory = null;
        }
    }
}
