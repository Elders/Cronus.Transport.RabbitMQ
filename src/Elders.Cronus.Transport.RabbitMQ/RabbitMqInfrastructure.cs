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
        private readonly PublicRabbitMqOptionsCollection publicRmqOptions;
        private readonly PublicMessagesRabbitMqNamer publicRabbitMqNamer;
        private readonly SignalMessagesRabbitMqNamer signalRabbitMqNamer;

        public RabbitMqInfrastructure(IOptionsMonitor<RabbitMqOptions> options, IOptionsMonitor<PublicRabbitMqOptionsCollection> publicOptions, PublicMessagesRabbitMqNamer rabbitMqNamer, SignalMessagesRabbitMqNamer signalRabbitMqNamer)
        {
            this.options = options.CurrentValue;
            this.publicRmqOptions = publicOptions.CurrentValue;
            this.publicRabbitMqNamer = rabbitMqNamer;
            this.signalRabbitMqNamer = signalRabbitMqNamer;
        }

        public void Initialize()
        {
            try
            {
                RabbitMqManagementClient priv = new RabbitMqManagementClient(options);
                CreateVHost(priv, options);

                foreach (var opt in publicRmqOptions.PublicClustersOptions)
                {
                    RabbitMqManagementClient pub = new RabbitMqManagementClient(opt);
                    CreateVHost(pub, opt);
                }

                if (ChecksIfHavePublishedLanguageConfigurations() == false)
                    logger.Warn(() => "Missing configurations for public rabbitMq.");

                foreach (PublicRabbitMqOptions publicSettings in publicRmqOptions.PublicClustersOptions)
                    CreatePublishedLanguageConnection(priv, publicSettings);
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => ex.Message)) { }
        }

        private bool ChecksIfHavePublishedLanguageConfigurations()
        {
            // We are sure that if missing configurations for public rabbitMq VHost by default equals "/"
            return publicRmqOptions.PublicClustersOptions.Any();
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

        private void CreatePublishedLanguageConnection(RabbitMqManagementClient downstreamClient, PublicRabbitMqOptions publicSettings)
        {
            var upstreams = publicSettings.GetUpstreamUris();
            if (upstreams.Any() == false)
                return;

            IEnumerable<string> publicExchangeNames = publicRabbitMqNamer.GetExchangeNames(typeof(IPublicEvent));
            IEnumerable<string> signalExchangeNames = signalRabbitMqNamer.GetExchangeNames(typeof(ISignal));
            IEnumerable<string> exchanges = publicExchangeNames.Concat(signalExchangeNames);

            foreach (var exchange in exchanges)
            {
                foreach (var upstream in upstreams)
                {
                    FederatedExchange federatedExchange = new FederatedExchange()
                    {
                        Name = publicSettings.VHost + $"--{exchange.ToLower()}",
                        Value = new FederatedExchange.ValueParameters()
                        {
                            Uri = upstream,
                            Exchange = exchange,
                            MaxHops = publicSettings.FederatedExchange.MaxHops
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
                    Name = publicSettings.VHost + $"--{exchange.ToLower()}",
                    Pattern = $"{exchange}$",
                    Priority = 1,
                    Definition = new Policy.DefinitionDto()
                    {
                        FederationUpstream = publicSettings.VHost + $"--{exchange.ToLower()}"
                    }
                };
                downstreamClient.CreatePolicy(policy, options.VHost);
            }
        }
    }
}
