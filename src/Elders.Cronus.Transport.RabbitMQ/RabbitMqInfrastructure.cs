﻿using Elders.Cronus.Transport.RabbitMQ.Management;
using Elders.Cronus.Transport.RabbitMQ.Management.Model;
using Elders.Cronus.Transport.RabbitMQ.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqInfrastructure
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(PublishedLanguageStartup));

        private readonly RabbitMqOptions options;
        private readonly PublicRabbitMqOptions publicOptions;
        private readonly PublicMessagesRabbitMqNamer rabbitMqNamer;

        public RabbitMqInfrastructure(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<PublicRabbitMqOptions> publicOptions, PublicMessagesRabbitMqNamer rabbitMqNamer)
        {
            this.options = options.CurrentValue;
            this.publicOptions = publicOptions.CurrentValue;
            this.rabbitMqNamer = rabbitMqNamer;
        }

        public void Initialize()
        {
            try
            {
                RabbitMqManagementClient priv = new RabbitMqManagementClient(options);
                CreateVHost(priv, options);

                RabbitMqManagementClient pub = new RabbitMqManagementClient(publicOptions);
                CreateVHost(pub, publicOptions);

                if (ChecksIfHavePublishedLanguageConfigurations())
                    logger.Warn(() => "Missing configurations for public rabbitMq.");
                else
                    CreatePublishedLanguageConnection(pub, publicOptions);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex, () => ex.Message);
            }
        }

        private bool ChecksIfHavePublishedLanguageConfigurations()
        {
            // We are sure that if missing configurations for public rabbitMq VHost by default equals "/"
            return publicOptions.VHost.Equals("/");
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
                foreach (var upstream in publicOptions.GetUpstreamUris())
                {
                    FederatedExchange federatedExchange = new FederatedExchange()
                    {
                        Name = publicOptions.VHost + "--events",
                        Value = new FederatedExchange.ValueParameters()
                        {
                            Uri = upstream,
                            Exchange = exchange,
                            MaxHops = publicOptions.FederatedExchange.MaxHops
                        }
                    };
                    client.CreateFederatedExchange(federatedExchange, options.VHost);
                }
            }

            foreach (var exchange in publicExchangeNames)
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
