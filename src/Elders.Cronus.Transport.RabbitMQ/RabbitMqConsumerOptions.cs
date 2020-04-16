using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumerOptions
    {
        [Range(1, int.MaxValue, ErrorMessage = "The configuration `Cronus:Transport:RabbitMq:Consumer:WorkersCount` allows values from 1 to 2147483647. For more information see here https://github.com/Elders/Cronus/blob/master/doc/Configuration.md")]
        public int WorkersCount { get; set; } = 5;
    }

    public class RabbitMqConsumerOptionsProvider : CronusOptionsProviderBase<RabbitMqConsumerOptions>
    {
        public const string SettingKey = "cronus:transport:rabbitmq:consumer";

        public RabbitMqConsumerOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(RabbitMqConsumerOptions options)
        {
            configuration.GetSection(SettingKey).Bind(options);
        }
    }
}
