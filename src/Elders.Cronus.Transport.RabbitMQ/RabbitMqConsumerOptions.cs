using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqConsumerOptions : IEquatable<RabbitMqConsumerOptions>
    {
        [Range(1, int.MaxValue, ErrorMessage = "The configuration `Cronus:Transport:RabbitMq:Consumer:WorkersCount` allows values from 1 to 2147483647. For more information see here https://github.com/Elders/Cronus/blob/master/doc/Configuration.md")]
        public int WorkersCount { get; set; } = 5;

        /// <summary>
        /// Drasticly changes the infrastructure behavior. This will create a separate queue per node and a message will be delivered to every node.
        /// </summary>
        public bool FanoutMode { get; set; } = false;

        public override string ToString()
        {
            return $"WorkersCount: {WorkersCount}";
        }

        public bool Equals([AllowNull] RabbitMqConsumerOptions other)
        {
            if (other is null)
                return false;

            return WorkersCount == other.WorkersCount && FanoutMode == other.FanoutMode;
        }

        public override bool Equals(object obj)
        {
            var other = (obj as RabbitMqConsumerOptions);
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(WorkersCount, FanoutMode);
        }

        public static bool operator ==(RabbitMqConsumerOptions left, RabbitMqConsumerOptions right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(RabbitMqConsumerOptions left, RabbitMqConsumerOptions right)
        {
            return !(left == right);
        }
    }

    public class RabbitMqConsumerOptionsProvider : CronusOptionsProviderBase<RabbitMqConsumerOptions>
    {
        public const string SectionKey = "cronus:transport:rabbitmq:consumer";

        public RabbitMqConsumerOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(RabbitMqConsumerOptions options)
        {
            configuration.GetSection(SectionKey).Bind(options);
        }
    }
}
