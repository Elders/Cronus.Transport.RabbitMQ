using Elders.Cronus.Pipeline.Config;

namespace Elders.Cronus.Pipeline.Hosts
{
    public static class CronusConfigurationRabbitMqExtensions
    {
        public static T WithDefaultPublishersWithRabbitMq<T>(this T self) where T : IConsumerSettings
        {
            self
                .UsePipelineEventPublisher()
                .UsePipelineCommandPublisher();
            return self;
        }
    }
}