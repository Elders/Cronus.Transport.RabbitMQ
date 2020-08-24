using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ
{
    [CronusStartup(Bootstraps.ExternalResource)]
    internal class PublishedLanguageStartup : ICronusStartup
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(PublishedLanguageStartup));

        private readonly RabbitMqOptions options;
        private readonly PublicRabbitMqOptions publicOptions;
        private readonly PublicMessagesRabbitMqNamer rabbitMqNamer;

        public PublishedLanguageStartup(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<PublicRabbitMqOptions> publicOptions, PublicMessagesRabbitMqNamer rabbitMqNamer)
        {
            this.options = options.CurrentValue;
            this.publicOptions = publicOptions.CurrentValue;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        public void Bootstrap()
        {
            try
            {
                RabbitMqManagementClient priv = new RabbitMqManagementClient(options);
                CreateVHost(priv, options);

                RabbitMqManagementClient pub = new RabbitMqManagementClient(publicOptions);
                CreateVHost(pub, publicOptions);
                CreatePublishedLanguageConnection(pub, publicOptions);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex, () => ex.Message);
            }
        }

        private void CreateVHost(RabbitMqManagementClient client, IRabbitMqOptions options)
        {
            if (!client.GetVHosts().Any(vh => vh.Name == options.VHost))
            {
                var vhost = client.CreateVirtualHost(options.VHost);
                var rabbitMqUser = client.GetUsers().SingleOrDefault(x => x.Name == options.Username);
                var permissionInfo = new PermissionInfo(rabbitMqUser, vhost);
                client.CreatePermission(permissionInfo);
            }
        }

        private void CreatePublishedLanguageConnection(RabbitMqManagementClient client, PublicRabbitMqOptions publicOptions)
        {
            IEnumerable<string> publicExchangeNames = rabbitMqNamer.GetExchangeNames(typeof(IPublicEvent));
            foreach (var exchange in publicExchangeNames)
            {
                FederatedExchange federatedExchange = new FederatedExchange()
                {
                    Name = publicOptions.VHost + "--events",
                    Value = new FederatedExchange.ValueParameters()
                    {
                        Uri = publicOptions.GetUpstreamUri(),
                        Exchange = exchange,
                        MaxHops = publicOptions.FederatedExchange.MaxHops
                    }
                };
                client.CreateFederatedExchange(federatedExchange, options.VHost);
            }

            IEnumerable<string> bcExchangeNames = rabbitMqNamer.GetExchangeNames(typeof(IPublicEvent));
            foreach (var exchange in bcExchangeNames)
            {
                Policy policy = new Policy()
                {
                    VHost = options.VHost,
                    Name = publicOptions.VHost + "--events",
                    Pattern = $"{exchange}$",
                    Definition = new Policy.DefinitionDto()
                    {
                        FederationUpstream = publicOptions.VHost + "--events"
                    }
                };
                client.CreatePolicy(policy, options.VHost);
            }
        }

    }
}
