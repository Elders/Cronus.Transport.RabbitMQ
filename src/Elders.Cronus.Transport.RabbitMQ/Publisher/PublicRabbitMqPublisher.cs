﻿using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublicRabbitMqPublisher : RabbitMqPublisherBase<IPublicEvent>
    {
        public PublicRabbitMqPublisher(ISerializer serializer, PublisherChannelResolver channelResolver, ITenantResolver<IMessage> tenantResolver, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<PublicRabbitMqOptions> options, PublicMessagesRabbitMqNamer publicRabbitMqNamer, ILogger<PublicRabbitMqPublisher> logger)
            : base(serializer, channelResolver, tenantResolver, boundedContext, options.CurrentValue, publicRabbitMqNamer, logger) { }

        protected override IBasicProperties BuildMessageProperties(IBasicProperties properties, CronusMessage message)
        {
            if (message.IsRepublished)
            {
                string boundedContext = message.Headers[MessageHeader.BoundedContext];
                string messageContractId = message.Payload.GetType().GetContractId();

                properties.Headers = new Dictionary<string, object>();

                if (message.GetPublishDelay() > 1000)
                    properties.Headers.Add("x-delay", message.GetPublishDelay());

                foreach (var recipientHandler in message.RecipientHandlers)
                {
                    properties.Headers.Add($"{messageContractId}@{recipientHandler}", boundedContext);
                }

                return properties;
            }
            else
            {
                return base.BuildMessageProperties(properties, message);
            }
        }
    }
}