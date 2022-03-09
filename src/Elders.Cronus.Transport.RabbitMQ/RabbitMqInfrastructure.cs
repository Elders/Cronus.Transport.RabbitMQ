using Elders.Cronus.Transport.RabbitMQ.Management;
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
        private readonly PublicMessagesRabbitMqNamer publicRabbitMqNamer;
        private readonly SignalMessagesRabbitMqNamer signalRabbitMqNamer;

        public RabbitMqInfrastructure(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<PublicRabbitMqOptions> publicOptions, PublicMessagesRabbitMqNamer rabbitMqNamer, SignalMessagesRabbitMqNamer signalRabbitMqNamer)
        {
            this.options = options.CurrentValue;
            this.publicOptions = publicOptions.CurrentValue;
            this.publicRabbitMqNamer = rabbitMqNamer;
            this.signalRabbitMqNamer = signalRabbitMqNamer;
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
                    CreatePublishedLanguageConnection(priv, publicOptions);
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

        private void CreatePublishedLanguageConnection(RabbitMqManagementClient downstreamClient, PublicRabbitMqOptions publicOptions)
        {
            IEnumerable<string> publicExchangeNames = publicRabbitMqNamer.GetExchangeNames(typeof(IPublicEvent));
            IEnumerable<string> signalExchangeNames = signalRabbitMqNamer.GetExchangeNames(typeof(ISignal));
            IEnumerable<string> exchanges = publicExchangeNames.Concat(signalExchangeNames);

            foreach (var exchange in exchanges)
            {
                foreach (var upstream in publicOptions.GetUpstreamUris())
                {
                    FederatedExchange federatedExchange = new FederatedExchange()
                    {
                        Name = publicOptions.VHost + $"--{exchange.ToLower()}",
                        Value = new FederatedExchange.ValueParameters()
                        {
                            Uri = upstream,
                            Exchange = exchange,
                            MaxHops = publicOptions.FederatedExchange.MaxHops
                        }
                    };
                    downstreamClient.CreateFederatedExchange(federatedExchange, options.VHost);
                }
            }

            foreach (var exchange in exchanges)
            {
                Policy policy = new Policy()
                {
                    VHost = options.VHost,
                    Name = publicOptions.VHost + $"--{exchange.ToLower()}",
                    Pattern = $"{exchange}$",
                    Priority = 1,
                    Definition = new Policy.DefinitionDto()
                    {
                        FederationUpstream = publicOptions.VHost + $"--{exchange.ToLower()}"
                    }
                };
                downstreamClient.CreatePolicy(policy, options.VHost);
            }
        }
    }
}
